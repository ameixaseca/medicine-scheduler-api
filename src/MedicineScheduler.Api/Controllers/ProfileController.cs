using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Profile;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Controllers;

/// <summary>Manage the authenticated user's profile, including settings and profile picture.</summary>
[ApiController]
[Route("profile")]
[Authorize]
[Produces("application/json")]
public class ProfileController(
    AppDbContext db,
    TimeProvider time,
    LogGenerationService logGenerationService,
    ILogger<ProfileController> logger) : ControllerBase
{
    private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
    private const long MaxPictureSizeBytes = 2 * 1024 * 1024; // 2 MB

    /// <summary>Get the profile of the authenticated user.</summary>
    /// <response code="200">Returns the user profile.</response>
    /// <response code="404">User not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get()
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound();
        return Ok(ToResponse(user));
    }

    /// <summary>Update name, timezone and notification preference.</summary>
    /// <remarks>
    /// When the timezone changes, all pending future logs are deleted and regenerated
    /// using the new timezone in a single transaction.
    /// Valid values for <c>notificationPreference</c>: <c>push</c>, <c>alarm</c>, <c>both</c>.
    /// </remarks>
    /// <response code="200">Profile updated.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">User not found.</response>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest req)
    {
        var userId = User.GetUserId();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var timezoneChanged = user.Timezone != req.Timezone;
        user.Name = req.Name;
        user.NotificationPreference = Enum.Parse<NotificationPreference>(req.NotificationPreference, ignoreCase: true);
        user.Timezone = req.Timezone;

        if (timezoneChanged)
        {
            var nowUtc = time.GetUtcNow().UtcDateTime;
            var newTz = TZConvert.GetTimeZoneInfo(req.Timezone);

            await using var tx = await db.Database.BeginTransactionAsync();

            var futurePending = await db.MedicationLogs
                .Include(l => l.Medication).ThenInclude(m => m.Patient)
                .Where(l =>
                    l.Medication.Patient.UserId == userId &&
                    l.Status == LogStatus.Pending &&
                    l.ScheduledTime > nowUtc)
                .ToListAsync();
            db.MedicationLogs.RemoveRange(futurePending);

            var medications = await db.Medications
                .Include(m => m.Schedule)
                .Include(m => m.Snapshots.OrderByDescending(s => s.CreatedAt))
                .Where(m => m.Patient.UserId == userId && !m.IsDeleted)
                .ToListAsync();

            var generatedCount = 0;
            foreach (var med in medications.Where(m => m.Schedule != null))
            {
                var newLogs = logGenerationService.GenerateInitialLogs(med, med.Snapshots.First(), newTz, nowUtc);
                db.MedicationLogs.AddRange(newLogs);
                generatedCount += newLogs.Count;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            logger.LogInformation(
                "Profile updated for user {UserId}: timezone changed to {Timezone}, {DeletedCount} pending logs removed, {GeneratedCount} logs regenerated",
                userId, req.Timezone, futurePending.Count, generatedCount);
        }
        else
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Profile updated for user {UserId}", userId);
        }

        return Ok(ToResponse(user));
    }

    /// <summary>Upload a profile picture.</summary>
    /// <remarks>
    /// Accepts JPEG, PNG, WebP or GIF up to 2 MB.
    /// The image is stored as a base64 data URI and returned in the <c>profilePicture</c> field.
    /// </remarks>
    /// <response code="200">Picture updated. Returns the updated profile.</response>
    /// <response code="400">File missing, too large or unsupported format.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfileResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdatePicture(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxPictureSizeBytes)
            return BadRequest(new { error = "Image must be smaller than 2 MB." });

        if (!AllowedImageTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { error = "Only JPEG, PNG, WebP and GIF images are allowed." });

        var user = await db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound();

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        user.ProfilePicture = $"data:{file.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";
        await db.SaveChangesAsync();

        logger.LogInformation("Profile picture updated for user {UserId}", User.GetUserId());
        return Ok(ToResponse(user));
    }

    /// <summary>Remove the profile picture.</summary>
    /// <response code="200">Picture removed. Returns the updated profile.</response>
    /// <response code="404">User not found.</response>
    [HttpDelete("picture")]
    [ProducesResponseType(typeof(ProfileResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeletePicture()
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound();

        user.ProfilePicture = null;
        await db.SaveChangesAsync();

        logger.LogInformation("Profile picture removed for user {UserId}", User.GetUserId());
        return Ok(ToResponse(user));
    }

    private static ProfileResponse ToResponse(User user) =>
        new(user.Name, user.ProfilePicture, user.Timezone, user.NotificationPreference.ToString().ToLower());
}

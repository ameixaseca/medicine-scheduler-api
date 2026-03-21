using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Settings;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using MedicineScheduler.Api.Services;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("settings")]
[Authorize]
public class SettingsController(AppDbContext db, TimeProvider time, LogGenerationService logGenerationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound();
        return Ok(new SettingsDto(user.NotificationPreference.ToString().ToLower(), user.Timezone));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SettingsDto req)
    {
        var userId = User.GetUserId();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var timezoneChanged = user.Timezone != req.Timezone;
        user.NotificationPreference = Enum.Parse<NotificationPreference>(req.NotificationPreference, ignoreCase: true);
        user.Timezone = req.Timezone;

        if (timezoneChanged)
        {
            var nowUtc = time.GetUtcNow().UtcDateTime;
            var newTz = TZConvert.GetTimeZoneInfo(req.Timezone);

            // All log regeneration in a single transaction (spec requirement)
            await using var tx = await db.Database.BeginTransactionAsync();

            // Delete all pending future logs for this user
            var futurePending = await db.MedicationLogs
                .Include(l => l.Medication).ThenInclude(m => m.Patient)
                .Where(l =>
                    l.Medication.Patient.UserId == userId &&
                    l.Status == LogStatus.Pending &&
                    l.ScheduledTime > nowUtc)
                .ToListAsync();
            db.MedicationLogs.RemoveRange(futurePending);

            // Regenerate using new timezone
            var medications = await db.Medications
                .Include(m => m.Schedule)
                .Include(m => m.Snapshots.OrderByDescending(s => s.CreatedAt))
                .Where(m => m.Patient.UserId == userId && !m.IsDeleted)
                .ToListAsync();

            foreach (var med in medications.Where(m => m.Schedule != null))
            {
                var snapshot = med.Snapshots.First(); // already ordered descending
                var newLogs = logGenerationService.GenerateInitialLogs(med, snapshot, newTz, nowUtc);
                db.MedicationLogs.AddRange(newLogs);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            await db.SaveChangesAsync();
        }
        return Ok(new SettingsDto(user.NotificationPreference.ToString().ToLower(), user.Timezone));
    }
}

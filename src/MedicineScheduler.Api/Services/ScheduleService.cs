using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Schedule;
using MedicineScheduler.Api.Entities;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Services;

public class ScheduleService(AppDbContext db, TimeProvider time)
{
    public async Task<List<ScheduleItemResponse>> GetForDateAsync(DateOnly date, Guid userId)
    {
        var user = await db.Users.FindAsync(userId) ?? throw new KeyNotFoundException();
        var tz = TZConvert.GetTimeZoneInfo(user.Timezone);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(date.Year, date.Month, date.Day, 0, 0, 0), tz);
        var endUtc = startUtc.AddDays(1);

        var logs = await db.MedicationLogs
            .Include(l => l.Medication).ThenInclude(m => m.Patient)
            .Where(l =>
                l.ScheduledTime >= startUtc &&
                l.ScheduledTime < endUtc &&
                l.Medication.Patient.UserId == userId &&
                !l.Medication.IsDeleted &&
                !l.Medication.Patient.IsDeleted)
            .OrderBy(l => l.ScheduledTime)
            .ToListAsync();

        return logs.Select(l => ToItem(l, tz)).ToList();
    }

    public async Task<LogActionResponse> ConfirmAsync(Guid logId, Guid userId)
    {
        var log = await FindOrThrowAsync(logId, userId);
        log.Status = LogStatus.Taken;
        log.TakenAt = time.GetUtcNow().UtcDateTime;
        log.SkippedBy = null;
        await db.SaveChangesAsync();
        return ToAction(log);
    }

    public async Task<LogActionResponse> SkipAsync(Guid logId, Guid userId)
    {
        var log = await FindOrThrowAsync(logId, userId);
        log.Status = LogStatus.Skipped;
        log.SkippedBy = Entities.SkippedBy.Caregiver;
        await db.SaveChangesAsync();
        return ToAction(log);
    }

    private async Task<MedicationLog> FindOrThrowAsync(Guid logId, Guid userId)
    {
        var log = await db.MedicationLogs
            .Include(l => l.Medication).ThenInclude(m => m.Patient)
            .SingleOrDefaultAsync(l => l.Id == logId);
        if (log == null) throw new KeyNotFoundException();
        if (log.Medication.Patient.UserId != userId) throw new UnauthorizedAccessException();
        return log;
    }

    private static ScheduleItemResponse ToItem(MedicationLog l, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(l.ScheduledTime, tz);
        return new ScheduleItemResponse(
            l.Id,
            l.ScheduledTime,
            local.ToString("HH:mm"),
            l.Status.ToString().ToLower(),
            l.SkippedBy?.ToString().ToLower(),
            new PatientSummary(l.Medication.Patient.Id, l.Medication.Patient.Name),
            new MedicationSummary(l.Medication.Id, l.Medication.Name,
                l.Medication.Dosage, l.Medication.Unit, l.Medication.ApplicationMethod));
    }

    private static LogActionResponse ToAction(MedicationLog l) =>
        new(l.Id, l.Status.ToString().ToLower(), l.TakenAt, l.SkippedBy?.ToString().ToLower());
}

using MedicineScheduler.Api.Entities;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Services;

public class LogGenerationService
{
    /// <summary>
    /// Generates MedicationLog entries for a given local calendar date.
    /// If sameDay is true, skips times whose UTC equivalent is at or before nowUtc.
    /// </summary>
    public List<MedicationLog> GenerateLogsForDate(
        Medication medication,
        MedicationScheduleSnapshot snapshot,
        DateOnly date,
        TimeZoneInfo tz,
        DateTime nowUtc,
        bool sameDay = false)
    {
        if (medication.EndDate.HasValue && date > medication.EndDate.Value)
            return [];

        var logs = new List<MedicationLog>();

        foreach (var timeStr in snapshot.Times)
        {
            var time = TimeOnly.ParseExact(timeStr, "HH:mm");
            var localDt = new DateTime(date.Year, date.Month, date.Day,
                time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
            var utcDt = TimeZoneInfo.ConvertTimeToUtc(localDt, tz);

            if (sameDay && utcDt <= nowUtc)
                continue;

            logs.Add(new MedicationLog
            {
                Id = Guid.NewGuid(),
                ScheduledTime = DateTime.SpecifyKind(utcDt, DateTimeKind.Utc),
                Status = LogStatus.Pending,
                MedicationId = medication.Id,
                MedicationScheduleSnapshotId = snapshot.Id
            });
        }

        return logs;
    }

    /// <summary>
    /// Generates logs for the current local date (same-day, from now forward)
    /// and optionally tomorrow if the 23:00 threshold has been crossed.
    /// </summary>
    public List<MedicationLog> GenerateInitialLogs(
        Medication medication,
        MedicationScheduleSnapshot snapshot,
        TimeZoneInfo tz,
        DateTime nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var today = DateOnly.FromDateTime(localNow);

        var logs = GenerateLogsForDate(medication, snapshot, today, tz, nowUtc, sameDay: true);

        if (localNow.Hour >= 23)
        {
            var tomorrow = today.AddDays(1);
            logs.AddRange(GenerateLogsForDate(medication, snapshot, tomorrow, tz, nowUtc, sameDay: false));
        }

        return logs;
    }
}

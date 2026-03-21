using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Services;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Jobs;

public class SchedulerJob(
    IServiceScopeFactory scopeFactory,
    LogGenerationService logGenerationService,
    TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SchedulerJob] Error: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    public async Task RunOnceAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var nowUtc = time.GetUtcNow().UtcDateTime;

        await DispatchNotificationsAsync(db, pushService, nowUtc);
        await AutoSkipOverdueAsync(db, nowUtc);
        await GenerateNextDayLogsAsync(db, nowUtc);
    }

    private async Task DispatchNotificationsAsync(
        AppDbContext db, IPushNotificationService pushService, DateTime nowUtc)
    {
        var windowStart = nowUtc.AddMinutes(-1);
        var windowEnd = nowUtc.AddMinutes(2);

        var pendingLogs = await db.MedicationLogs
            .Include(l => l.Medication).ThenInclude(m => m.Patient).ThenInclude(p => p.User)
            .Include(l => l.Snapshot)
            .Where(l =>
                l.ScheduledTime >= windowStart &&
                l.ScheduledTime <= windowEnd &&
                l.NotificationSentAt == null &&
                l.Status == LogStatus.Pending &&
                !l.Medication.IsDeleted &&
                !l.Medication.Patient.IsDeleted)
            .ToListAsync();

        foreach (var log in pendingLogs)
        {
            var user = log.Medication.Patient.User;
            if (user.NotificationPreference == NotificationPreference.Alarm) continue;

            await pushService.SendAsync(user,
                $"Hora do medicamento: {log.Medication.Name}",
                $"{log.Medication.Dosage} {log.Medication.Unit} — {log.Medication.ApplicationMethod}");

            log.NotificationSentAt = nowUtc;
        }

        await db.SaveChangesAsync();
    }

    private static async Task AutoSkipOverdueAsync(AppDbContext db, DateTime nowUtc)
    {
        var cutoff = nowUtc.AddMinutes(-30);

        var overdue = await db.MedicationLogs
            .Where(l =>
                l.Status == LogStatus.Pending &&
                l.ScheduledTime < cutoff &&
                !l.Medication.IsDeleted)
            .ToListAsync();

        foreach (var log in overdue)
        {
            log.Status = LogStatus.Skipped;
            log.SkippedBy = SkippedBy.Auto;
        }

        await db.SaveChangesAsync();
    }

    private async Task GenerateNextDayLogsAsync(AppDbContext db, DateTime nowUtc)
    {
        var users = await db.Users.Where(u => !u.IsDeleted).ToListAsync();

        foreach (var user in users)
        {
            var tz = TZConvert.GetTimeZoneInfo(user.Timezone);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            if (localNow.Hour < 23) continue;

            var tomorrow = DateOnly.FromDateTime(localNow).AddDays(1);

            var alreadyHasLogs = await db.MedicationLogs
                .Include(l => l.Medication).ThenInclude(m => m.Patient)
                .AnyAsync(l =>
                    l.Medication.Patient.UserId == user.Id &&
                    DateOnly.FromDateTime(l.ScheduledTime) == tomorrow);

            if (alreadyHasLogs) continue;

            var medications = await db.Medications
                .Include(m => m.Schedule)
                .Include(m => m.Snapshots.OrderByDescending(s => s.CreatedAt))
                .Where(m => m.Patient.UserId == user.Id && !m.IsDeleted)
                .ToListAsync();

            var newLogs = new List<MedicationLog>();
            foreach (var med in medications.Where(m => m.Schedule != null))
            {
                var snapshot = med.Snapshots.First(); // already ordered descending
                newLogs.AddRange(logGenerationService.GenerateLogsForDate(
                    med, snapshot, tomorrow, tz, nowUtc, sameDay: false));
            }

            db.MedicationLogs.AddRange(newLogs);
        }

        await db.SaveChangesAsync();
    }
}

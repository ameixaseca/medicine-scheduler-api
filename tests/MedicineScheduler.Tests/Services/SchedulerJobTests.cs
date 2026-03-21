using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Jobs;
using MedicineScheduler.Api.Services;
using Moq;

namespace MedicineScheduler.Tests.Services;

public class SchedulerJobTests
{
    private AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SchedulerJob CreateJob(AppDbContext db, IPushNotificationService push, DateTime nowUtc)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(push);
        var sp = services.BuildServiceProvider();
        return new SchedulerJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new LogGenerationService(),
            new FakeTimeProvider(nowUtc));
    }

    private async Task<(User user, Medication med, MedicationScheduleSnapshot snap)>
        SeedAsync(AppDbContext db, string timezone = "UTC")
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "u@t.com", PasswordHash = "x",
            Name = "U", Timezone = timezone,
            NotificationPreference = NotificationPreference.Push
        };
        var patient = new Patient
        {
            Id = Guid.NewGuid(), Name = "P", DateOfBirth = new DateOnly(2000, 1, 1),
            UserId = user.Id, User = user
        };
        var med = new Medication
        {
            Id = Guid.NewGuid(), Name = "M", Dosage = "10", Unit = "mg",
            ApplicationMethod = "oral", StartDate = DateOnly.FromDateTime(DateTime.Today),
            PatientId = patient.Id, Patient = patient
        };
        var snap = new MedicationScheduleSnapshot
        {
            Id = Guid.NewGuid(), FrequencyPerDay = 1, Times = ["08:00"],
            CreatedAt = DateTime.UtcNow, MedicationId = med.Id, Medication = med
        };
        db.Users.Add(user);
        db.Patients.Add(patient);
        db.Medications.Add(med);
        db.MedicationScheduleSnapshots.Add(snap);
        await db.SaveChangesAsync();
        return (user, med, snap);
    }

    [Fact]
    public async Task Step1_DispatchesNotification_WhenInWindow()
    {
        var db = CreateDb();
        var pushMock = new Mock<IPushNotificationService>();
        var (user, med, snap) = await SeedAsync(db);
        var nowUtc = new DateTime(2026, 3, 21, 11, 0, 0, DateTimeKind.Utc);

        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = nowUtc.AddSeconds(30), // within +2 min window
            Status = LogStatus.Pending,
            MedicationId = med.Id, Medication = med,
            MedicationScheduleSnapshotId = snap.Id, Snapshot = snap
        });
        await db.SaveChangesAsync();

        var job = CreateJob(db, pushMock.Object, nowUtc);
        await job.RunOnceAsync();

        pushMock.Verify(p => p.SendAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        var log = await db.MedicationLogs.FirstAsync();
        Assert.NotNull(log.NotificationSentAt);
    }

    [Fact]
    public async Task Step1_DoesNotDispatch_WhenAlreadySent()
    {
        var db = CreateDb();
        var pushMock = new Mock<IPushNotificationService>();
        var (user, med, snap) = await SeedAsync(db);
        var nowUtc = new DateTime(2026, 3, 21, 11, 0, 0, DateTimeKind.Utc);

        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = nowUtc.AddSeconds(30),
            Status = LogStatus.Pending,
            NotificationSentAt = nowUtc.AddMinutes(-1), // already sent
            MedicationId = med.Id, Medication = med,
            MedicationScheduleSnapshotId = snap.Id, Snapshot = snap
        });
        await db.SaveChangesAsync();

        var job = CreateJob(db, pushMock.Object, nowUtc);
        await job.RunOnceAsync();

        pushMock.Verify(p => p.SendAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Step2_AutoSkips_OverdueEntries()
    {
        var db = CreateDb();
        var (user, med, snap) = await SeedAsync(db);
        var nowUtc = new DateTime(2026, 3, 21, 11, 0, 0, DateTimeKind.Utc);

        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = nowUtc.AddMinutes(-35), // overdue > 30 min
            Status = LogStatus.Pending,
            MedicationId = med.Id, Medication = med,
            MedicationScheduleSnapshotId = snap.Id, Snapshot = snap
        });
        await db.SaveChangesAsync();

        var job = CreateJob(db, Mock.Of<IPushNotificationService>(), nowUtc);
        await job.RunOnceAsync();

        var log = await db.MedicationLogs.FirstAsync();
        Assert.Equal(LogStatus.Skipped, log.Status);
        Assert.Equal(SkippedBy.Auto, log.SkippedBy);
    }

    [Fact]
    public async Task Step3_GeneratesNextDayLogs_WhenAfter23h()
    {
        var db = CreateDb();
        var (user, med, snap) = await SeedAsync(db, timezone: "UTC");
        // 23:30 UTC for a UTC user → triggers next-day generation
        var nowUtc = new DateTime(2026, 3, 21, 23, 30, 0, DateTimeKind.Utc);

        db.MedicationSchedules.Add(new MedicationSchedule
        {
            Id = Guid.NewGuid(), FrequencyPerDay = 1,
            Times = ["08:00"], MedicationId = med.Id
        });
        await db.SaveChangesAsync();

        var job = CreateJob(db, Mock.Of<IPushNotificationService>(), nowUtc);
        await job.RunOnceAsync();

        var tomorrow = new DateOnly(2026, 3, 22);
        var tomorrowLogs = await db.MedicationLogs
            .Where(l => DateOnly.FromDateTime(l.ScheduledTime) == tomorrow)
            .ToListAsync();
        Assert.NotEmpty(tomorrowLogs);
    }
}

public class FakeTimeProvider(DateTime fixedUtc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(fixedUtc, TimeSpan.Zero);
}

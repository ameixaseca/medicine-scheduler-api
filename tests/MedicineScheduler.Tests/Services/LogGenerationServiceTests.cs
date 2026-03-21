using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Tests.Services;

public class LogGenerationServiceTests
{
    private static TimeZoneInfo Brasilia =>
        TimeZoneConverter.TZConvert.GetTimeZoneInfo("America/Sao_Paulo");

    private static MedicationScheduleSnapshot MakeSnapshot(params string[] times) => new()
    {
        Id = Guid.NewGuid(),
        MedicationId = Guid.NewGuid(),
        FrequencyPerDay = times.Length,
        Times = [.. times],
        CreatedAt = DateTime.UtcNow
    };

    private static Medication MakeMedication(Guid snapshotMedicationId, DateOnly? endDate = null) => new()
    {
        Id = snapshotMedicationId,
        Name = "Test",
        Dosage = "10",
        Unit = "mg",
        ApplicationMethod = "oral",
        StartDate = DateOnly.FromDateTime(DateTime.Today),
        EndDate = endDate
    };

    [Fact]
    public void GenerateForDate_FutureDate_AllTimesIncluded()
    {
        var snapshot = MakeSnapshot("08:00", "14:00", "20:00");
        var med = MakeMedication(snapshot.MedicationId);
        var svc = new LogGenerationService();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var nowUtc = DateTime.UtcNow;

        var logs = svc.GenerateLogsForDate(med, snapshot, tomorrow, Brasilia, nowUtc, sameDay: false);

        Assert.Equal(3, logs.Count);
    }

    [Fact]
    public void GenerateForDate_SameDay_SkipsPastTimes()
    {
        var snapshot = MakeSnapshot("08:00", "23:59");
        var med = MakeMedication(snapshot.MedicationId);
        var svc = new LogGenerationService();

        // nowUtc is 12:00 Brasilia time (UTC-3) = 15:00 UTC
        // 08:00 local = 11:00 UTC → past
        // 23:59 local = 02:59 UTC next day → future
        var today = new DateOnly(2026, 3, 21);
        var nowUtc = new DateTime(2026, 3, 21, 15, 0, 0, DateTimeKind.Utc);

        var logs = svc.GenerateLogsForDate(med, snapshot, today, Brasilia, nowUtc, sameDay: true);

        Assert.Single(logs); // only 23:59
        Assert.Equal(new DateTime(2026, 3, 22, 2, 59, 0, DateTimeKind.Utc), logs[0].ScheduledTime);
    }

    [Fact]
    public void GenerateForDate_PastStartDate_DoesNotBackfill()
    {
        var snapshot = MakeSnapshot("08:00", "20:00");
        var med = MakeMedication(snapshot.MedicationId);
        var svc = new LogGenerationService();

        var yesterday = new DateOnly(2026, 3, 20);
        var nowUtc = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc);

        var logs = svc.GenerateLogsForDate(med, snapshot, yesterday, Brasilia, nowUtc, sameDay: true);

        Assert.Empty(logs);
    }

    [Fact]
    public void GenerateForDate_EndDateExcludes_DateAfterEndDate()
    {
        var snapshot = MakeSnapshot("08:00");
        var endDate = new DateOnly(2026, 3, 21);
        var med = MakeMedication(snapshot.MedicationId, endDate: endDate);
        var svc = new LogGenerationService();

        var dayAfterEnd = new DateOnly(2026, 3, 22);
        var nowUtc = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);

        var logs = svc.GenerateLogsForDate(med, snapshot, dayAfterEnd, Brasilia, nowUtc, sameDay: false);

        Assert.Empty(logs);
    }

    [Fact]
    public void GenerateForDate_EndDateIncludes_DateOnEndDate()
    {
        var snapshot = MakeSnapshot("08:00");
        var endDate = new DateOnly(2026, 3, 21);
        var med = MakeMedication(snapshot.MedicationId, endDate: endDate);
        var svc = new LogGenerationService();

        var nowUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);

        var logs = svc.GenerateLogsForDate(med, snapshot, endDate, Brasilia, nowUtc, sameDay: false);

        Assert.Single(logs);
    }

    [Fact]
    public void GenerateForDate_ScheduledTimeIsUtc()
    {
        var snapshot = MakeSnapshot("08:00");
        var med = MakeMedication(snapshot.MedicationId);
        var svc = new LogGenerationService();

        var date = new DateOnly(2026, 3, 21);
        var nowUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);

        var logs = svc.GenerateLogsForDate(med, snapshot, date, Brasilia, nowUtc, sameDay: false);

        // Brasilia is UTC-3: 08:00 local = 11:00 UTC
        Assert.Single(logs);
        Assert.Equal(new DateTime(2026, 3, 21, 11, 0, 0, DateTimeKind.Utc), logs[0].ScheduledTime);
        Assert.Equal(DateTimeKind.Utc, logs[0].ScheduledTime.Kind);
    }
}

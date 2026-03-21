using Microsoft.EntityFrameworkCore;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Tests.Services;

public class ScheduleServiceTests
{
    private AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task<(MedicationLog log, Guid userId)> SeedLogAsync(
        AppDbContext db, LogStatus status = LogStatus.Pending,
        SkippedBy? skippedBy = null)
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId, Email = "u@t.com", PasswordHash = "x",
            Name = "U", Timezone = "UTC"
        };
        var patient = new Patient
        {
            Id = Guid.NewGuid(), Name = "P",
            DateOfBirth = new DateOnly(2000, 1, 1),
            UserId = userId, User = user
        };
        var medication = new Medication
        {
            Id = Guid.NewGuid(), Name = "M", Dosage = "10", Unit = "mg",
            ApplicationMethod = "oral", StartDate = DateOnly.FromDateTime(DateTime.Today),
            PatientId = patient.Id, Patient = patient
        };
        var snapshot = new MedicationScheduleSnapshot
        {
            Id = Guid.NewGuid(), FrequencyPerDay = 1,
            Times = ["08:00"], CreatedAt = DateTime.UtcNow,
            MedicationId = medication.Id, Medication = medication
        };
        var log = new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddHours(-1),
            Status = status,
            SkippedBy = skippedBy,
            MedicationId = medication.Id, Medication = medication,
            MedicationScheduleSnapshotId = snapshot.Id, Snapshot = snapshot
        };
        db.Users.Add(user);
        db.Patients.Add(patient);
        db.Medications.Add(medication);
        db.MedicationScheduleSnapshots.Add(snapshot);
        db.MedicationLogs.Add(log);
        await db.SaveChangesAsync();
        return (log, userId);
    }

    [Fact]
    public async Task Confirm_Pending_SetsTaken()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, userId) = await SeedLogAsync(db);

        var result = await svc.ConfirmAsync(log.Id, userId);

        Assert.Equal("taken", result.Status);
        Assert.NotNull(result.TakenAt);
    }

    [Fact]
    public async Task Confirm_AfterAutoSkip_OverridesSkip()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, userId) = await SeedLogAsync(db, LogStatus.Skipped, SkippedBy.Auto);

        var result = await svc.ConfirmAsync(log.Id, userId);

        Assert.Equal("taken", result.Status);
    }

    [Fact]
    public async Task Skip_Pending_SetsSkippedByCaregiver()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, userId) = await SeedLogAsync(db);

        var result = await svc.SkipAsync(log.Id, userId);

        Assert.Equal("skipped", result.Status);
        Assert.Equal("caregiver", result.SkippedBy);
    }

    [Fact]
    public async Task Skip_AfterAutoSkip_UpdatesSkippedByToCaregiver()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, userId) = await SeedLogAsync(db, LogStatus.Skipped, SkippedBy.Auto);

        var result = await svc.SkipAsync(log.Id, userId);

        Assert.Equal("caregiver", result.SkippedBy);
    }

    [Fact]
    public async Task Confirm_CrossUser_ThrowsUnauthorized()
    {
        var db = CreateDb();
        var svc = new ScheduleService(db, TimeProvider.System);
        var (log, _) = await SeedLogAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ConfirmAsync(log.Id, Guid.NewGuid()));
    }
}

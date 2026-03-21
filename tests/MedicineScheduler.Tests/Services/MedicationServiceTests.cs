using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Medications;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Tests.Services;

public class MedicationServiceTests
{
    private (AppDbContext, MedicationService) Setup()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(opts);
        var logSvc = new LogGenerationService();
        var svc = new MedicationService(db, logSvc, TimeProvider.System);
        return (db, svc);
    }

    private async Task<(Patient, User)> SeedUserAndPatient(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "u@t.com", PasswordHash = "x",
            Name = "User", Timezone = "America/Sao_Paulo"
        };
        var patient = new Patient
        {
            Id = Guid.NewGuid(), Name = "Patient", DateOfBirth = new DateOnly(2000, 1, 1),
            UserId = user.Id, User = user
        };
        db.Users.Add(user);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return (patient, user);
    }

    [Fact]
    public async Task Create_CreatesSnapshotAndLogsAndSchedule()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var req = new MedicationRequest("Losartana", "50", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00", "20:00"]);

        var response = await svc.CreateAsync(patient.Id, req, user.Id);

        Assert.Equal(2, response.Schedule.FrequencyPerDay);
        Assert.Equal(1, await db.MedicationScheduleSnapshots.CountAsync());
        Assert.Equal(1, await db.MedicationSchedules.CountAsync());
    }

    [Fact]
    public async Task Create_FrequencyPerDayDerivedFromTimesCount()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var req = new MedicationRequest("Med", "10", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00", "14:00", "20:00"]);

        var response = await svc.CreateAsync(patient.Id, req, user.Id);

        Assert.Equal(3, response.Schedule.FrequencyPerDay);
    }

    [Fact]
    public async Task Update_CreatesNewSnapshot_DeletesPendingFutureLogs()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var createReq = new MedicationRequest("Med", "10", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00"]);
        var med = await svc.CreateAsync(patient.Id, createReq, user.Id);

        // Manually add a future pending log
        var medEntity = await db.Medications.Include(m => m.Snapshots).FirstAsync();
        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddDays(1),
            Status = LogStatus.Pending,
            MedicationId = medEntity.Id,
            MedicationScheduleSnapshotId = medEntity.Snapshots.First().Id
        });
        await db.SaveChangesAsync();

        var updateReq = new MedicationRequest("Med", "20", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["09:00", "21:00"]);
        await svc.UpdateAsync(med.Id, updateReq, user.Id);

        Assert.Equal(2, await db.MedicationScheduleSnapshots.CountAsync());
        // Pending future log should be deleted
        Assert.DoesNotContain(await db.MedicationLogs.ToListAsync(),
            l => l.Status == LogStatus.Pending && l.ScheduledTime > DateTime.UtcNow);
    }

    [Fact]
    public async Task Delete_SoftDeletes_HardDeletesPendingFutureLogs_RetainsTakenAndSkipped()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var req = new MedicationRequest("Med", "10", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00"]);
        var med = await svc.CreateAsync(patient.Id, req, user.Id);

        var medEntity = await db.Medications.Include(m => m.Snapshots).FirstAsync();
        var snapId = medEntity.Snapshots.First().Id;

        // Future pending log — should be hard-deleted
        db.MedicationLogs.Add(new MedicationLog
        {
            Id = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddDays(1),
            Status = LogStatus.Pending,
            MedicationId = medEntity.Id,
            MedicationScheduleSnapshotId = snapId
        });
        // Past taken log — should be RETAINED
        var takenLogId = Guid.NewGuid();
        db.MedicationLogs.Add(new MedicationLog
        {
            Id = takenLogId,
            ScheduledTime = DateTime.UtcNow.AddHours(-2),
            Status = LogStatus.Taken,
            TakenAt = DateTime.UtcNow.AddHours(-2),
            MedicationId = medEntity.Id,
            MedicationScheduleSnapshotId = snapId
        });
        await db.SaveChangesAsync();

        await svc.DeleteAsync(med.Id, user.Id);

        var medAfter = await db.Medications.FindAsync(med.Id);
        Assert.True(medAfter!.IsDeleted);
        Assert.Empty(await db.MedicationLogs.Where(l =>
            l.MedicationId == med.Id && l.Status == LogStatus.Pending &&
            l.ScheduledTime > DateTime.UtcNow).ToListAsync());
        Assert.NotNull(await db.MedicationLogs.FindAsync(takenLogId));
    }

    [Fact]
    public async Task Get_CrossUser_ThrowsUnauthorized()
    {
        var (db, svc) = Setup();
        var (patient, user) = await SeedUserAndPatient(db);
        var req = new MedicationRequest("Med", "10", "mg", "oral",
            DateOnly.FromDateTime(DateTime.Today), null, ["08:00"]);
        var med = await svc.CreateAsync(patient.Id, req, user.Id);

        var otherUserId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetAsync(med.Id, otherUserId));
    }
}

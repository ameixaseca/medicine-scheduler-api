using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Medications;
using MedicineScheduler.Api.Entities;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Services;

public class MedicationService(AppDbContext db, LogGenerationService logSvc, TimeProvider time, ILogger<MedicationService> logger)
{
    public async Task<List<MedicationResponse>> GetAllForPatientAsync(Guid patientId, Guid userId)
    {
        await VerifyPatientOwnershipAsync(patientId, userId);
        return await db.Medications
            .Include(m => m.Schedule)
            .Where(m => m.PatientId == patientId && !m.IsDeleted)
            .Select(m => ToResponse(m))
            .ToListAsync();
    }

    public async Task<MedicationResponse> GetAsync(Guid id, Guid userId)
    {
        var med = await FindOrThrowAsync(id, userId);
        await db.Entry(med).Reference(m => m.Schedule).LoadAsync();
        return ToResponse(med);
    }

    public async Task<MedicationResponse> CreateAsync(Guid patientId, MedicationRequest req, Guid userId)
    {
        await VerifyPatientOwnershipAsync(patientId, userId);

        var nowUtc = time.GetUtcNow().UtcDateTime;
        var patient = await db.Patients.Include(p => p.User).SingleAsync(p => p.Id == patientId);
        var tz = TZConvert.GetTimeZoneInfo(patient.User.Timezone);

        var medication = new Medication
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Dosage = req.Dosage,
            Unit = req.Unit,
            ApplicationMethod = req.ApplicationMethod,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            PatientId = patientId
        };

        var snapshot = new MedicationScheduleSnapshot
        {
            Id = Guid.NewGuid(),
            FrequencyPerDay = req.Times.Count,
            Times = req.Times,
            CreatedAt = nowUtc,
            MedicationId = medication.Id
        };

        var schedule = new MedicationSchedule
        {
            Id = Guid.NewGuid(),
            FrequencyPerDay = req.Times.Count,
            Times = req.Times,
            MedicationId = medication.Id
        };

        var logs = logSvc.GenerateInitialLogs(medication, snapshot, tz, nowUtc);

        await using var tx = await db.Database.BeginTransactionAsync();
        db.Medications.Add(medication);
        db.MedicationScheduleSnapshots.Add(snapshot);
        db.MedicationSchedules.Add(schedule);
        db.MedicationLogs.AddRange(logs);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        logger.LogInformation(
            "Medication {MedicationId} created for patient {PatientId} by user {UserId}: {LogCount} logs generated",
            medication.Id, patientId, userId, logs.Count);

        medication.Schedule = schedule;
        return ToResponse(medication);
    }

    public async Task<MedicationResponse> UpdateAsync(Guid id, MedicationRequest req, Guid userId)
    {
        var med = await FindOrThrowAsync(id, userId);
        var patient = await db.Patients.Include(p => p.User).SingleAsync(p => p.Id == med.PatientId);
        var tz = TZConvert.GetTimeZoneInfo(patient.User.Timezone);
        var nowUtc = time.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync();

        // 1. Create new snapshot
        var snapshot = new MedicationScheduleSnapshot
        {
            Id = Guid.NewGuid(),
            FrequencyPerDay = req.Times.Count,
            Times = req.Times,
            CreatedAt = nowUtc,
            MedicationId = med.Id
        };
        db.MedicationScheduleSnapshots.Add(snapshot);

        // 2. Update medication fields and schedule in-place
        med.Name = req.Name;
        med.Dosage = req.Dosage;
        med.Unit = req.Unit;
        med.ApplicationMethod = req.ApplicationMethod;
        med.StartDate = req.StartDate;
        med.EndDate = req.EndDate;

        var schedule = await db.MedicationSchedules.SingleAsync(s => s.MedicationId == med.Id);
        schedule.FrequencyPerDay = req.Times.Count;
        schedule.Times = req.Times;

        // 3. Delete pending future logs
        var pendingFutureLogs = await db.MedicationLogs
            .Where(l => l.MedicationId == med.Id && l.Status == LogStatus.Pending && l.ScheduledTime > nowUtc)
            .ToListAsync();
        db.MedicationLogs.RemoveRange(pendingFutureLogs);

        await db.SaveChangesAsync();

        // 4. Generate same-day + next-day logs
        var newLogs = logSvc.GenerateInitialLogs(med, snapshot, tz, nowUtc);
        db.MedicationLogs.AddRange(newLogs);
        await db.SaveChangesAsync();

        await tx.CommitAsync();

        logger.LogInformation(
            "Medication {MedicationId} updated by user {UserId}: {DeletedCount} pending logs removed, {GeneratedCount} new logs generated",
            id, userId, pendingFutureLogs.Count, newLogs.Count);

        med.Schedule = schedule;
        return ToResponse(med);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var med = await FindOrThrowAsync(id, userId);
        var nowUtc = time.GetUtcNow().UtcDateTime;

        var pendingFutureLogs = await db.MedicationLogs
            .Where(l => l.MedicationId == id && l.Status == LogStatus.Pending && l.ScheduledTime > nowUtc)
            .ToListAsync();
        db.MedicationLogs.RemoveRange(pendingFutureLogs);

        med.IsDeleted = true;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Medication {MedicationId} deleted by user {UserId}: {DeletedCount} pending logs removed",
            id, userId, pendingFutureLogs.Count);
    }

    private async Task VerifyPatientOwnershipAsync(Guid patientId, Guid userId)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);
        if (patient == null) throw new KeyNotFoundException();
        if (patient.UserId != userId) throw new UnauthorizedAccessException();
    }

    private async Task<Medication> FindOrThrowAsync(Guid id, Guid userId)
    {
        var med = await db.Medications
            .Include(m => m.Patient)
            .SingleOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        if (med == null) throw new KeyNotFoundException();
        if (med.Patient.UserId != userId) throw new UnauthorizedAccessException();
        return med;
    }

    private static MedicationResponse ToResponse(Medication m) => new(
        m.Id, m.Name, m.Dosage, m.Unit, m.ApplicationMethod, m.StartDate, m.EndDate,
        new MedicationScheduleDto(m.Schedule!.FrequencyPerDay, m.Schedule.Times));
}

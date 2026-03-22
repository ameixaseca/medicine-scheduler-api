using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Patients;
using MedicineScheduler.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedicineScheduler.Api.Services;

public class PatientService(AppDbContext db, ILogger<PatientService> logger)
{
    public async Task<List<PatientResponse>> GetAllAsync(Guid userId) =>
        await db.Patients
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => new PatientResponse(p.Id, p.Name, p.DateOfBirth, p.Notes))
            .ToListAsync();

    public async Task<PatientResponse> GetAsync(Guid id, Guid userId)
    {
        var patient = await FindOrThrowAsync(id, userId);
        return new PatientResponse(patient.Id, patient.Name, patient.DateOfBirth, patient.Notes);
    }

    public async Task<PatientResponse> CreateAsync(PatientRequest req, Guid userId)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            DateOfBirth = req.DateOfBirth,
            Notes = req.Notes,
            UserId = userId
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        logger.LogInformation("Patient {PatientId} created by user {UserId}", patient.Id, userId);

        return new PatientResponse(patient.Id, patient.Name, patient.DateOfBirth, patient.Notes);
    }

    public async Task<PatientResponse> UpdateAsync(Guid id, PatientRequest req, Guid userId)
    {
        var patient = await FindOrThrowAsync(id, userId);
        patient.Name = req.Name;
        patient.DateOfBirth = req.DateOfBirth;
        patient.Notes = req.Notes;
        await db.SaveChangesAsync();

        logger.LogInformation("Patient {PatientId} updated by user {UserId}", id, userId);

        return new PatientResponse(patient.Id, patient.Name, patient.DateOfBirth, patient.Notes);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var patient = await FindOrThrowAsync(id, userId);
        patient.IsDeleted = true;
        await db.SaveChangesAsync();

        logger.LogInformation("Patient {PatientId} deleted by user {UserId}", id, userId);
    }

    private async Task<Patient> FindOrThrowAsync(Guid id, Guid userId)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (patient == null) throw new KeyNotFoundException();
        if (patient.UserId != userId) throw new UnauthorizedAccessException();
        return patient;
    }
}

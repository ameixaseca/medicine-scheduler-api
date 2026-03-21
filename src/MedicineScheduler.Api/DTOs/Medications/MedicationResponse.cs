namespace MedicineScheduler.Api.DTOs.Medications;
public record MedicationResponse(
    Guid Id,
    string Name,
    string Dosage,
    string Unit,
    string ApplicationMethod,
    DateOnly StartDate,
    DateOnly? EndDate,
    MedicationScheduleDto Schedule);

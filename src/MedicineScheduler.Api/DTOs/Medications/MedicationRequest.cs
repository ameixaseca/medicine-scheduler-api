namespace MedicineScheduler.Api.DTOs.Medications;
public record MedicationRequest(
    string Name,
    string Dosage,
    string Unit,
    string ApplicationMethod,
    DateOnly StartDate,
    DateOnly? EndDate,
    List<string> Times);

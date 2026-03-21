namespace MedicineScheduler.Api.DTOs.Schedule;

public record PatientSummary(Guid Id, string Name);
public record MedicationSummary(Guid Id, string Name, string Dosage, string Unit, string ApplicationMethod);

public record ScheduleItemResponse(
    Guid LogId,
    DateTime ScheduledTime,
    string ScheduledTimeLocal,
    string Status,
    string? SkippedBy,
    PatientSummary Patient,
    MedicationSummary Medication);

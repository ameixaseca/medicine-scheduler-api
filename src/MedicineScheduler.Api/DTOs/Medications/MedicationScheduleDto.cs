namespace MedicineScheduler.Api.DTOs.Medications;
public record MedicationScheduleDto(int FrequencyPerDay, List<string> Times);

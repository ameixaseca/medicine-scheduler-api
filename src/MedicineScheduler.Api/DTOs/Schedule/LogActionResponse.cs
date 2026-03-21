namespace MedicineScheduler.Api.DTOs.Schedule;
public record LogActionResponse(Guid Id, string Status, DateTime? TakenAt, string? SkippedBy);

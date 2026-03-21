namespace MedicineScheduler.Api.DTOs.Patients;

public record PatientResponse(Guid Id, string Name, DateOnly DateOfBirth, string? Notes);

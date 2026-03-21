namespace MedicineScheduler.Api.DTOs.Patients;

public record PatientRequest(string Name, DateOnly DateOfBirth, string? Notes);

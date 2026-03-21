// src/MedicineScheduler.Api/Validators/PatientRequestValidator.cs
using FluentValidation;
using MedicineScheduler.Api.DTOs.Patients;

namespace MedicineScheduler.Api.Validators;

public class PatientRequestValidator : AbstractValidator<PatientRequest>
{
    public PatientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(DateOnly.FromDateTime(DateTime.Today));
    }
}

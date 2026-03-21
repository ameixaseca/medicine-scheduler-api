// src/MedicineScheduler.Api/Validators/MedicationRequestValidator.cs
using FluentValidation;
using MedicineScheduler.Api.DTOs.Medications;
using System.Text.RegularExpressions;

namespace MedicineScheduler.Api.Validators;

public partial class MedicationRequestValidator : AbstractValidator<MedicationRequest>
{
    [GeneratedRegex(@"^\d{2}:\d{2}$")]
    private static partial Regex HhMmRegex();

    public MedicationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dosage).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ApplicationMethod).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x).Must(x => x.EndDate == null || x.StartDate <= x.EndDate)
            .WithName("EndDate").WithMessage("EndDate must be >= StartDate.");
        RuleFor(x => x.Times).NotEmpty()
            .Must(t => t.Count >= 1 && t.Count <= 24).WithMessage("Times must have 1–24 entries.")
            .Must(t => t.All(s => HhMmRegex().IsMatch(s))).WithMessage("Each time must be HH:mm.")
            .Must(t => t.Distinct().Count() == t.Count).WithMessage("Times must be unique.");
    }
}

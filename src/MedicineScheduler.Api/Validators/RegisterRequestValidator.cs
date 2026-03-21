// src/MedicineScheduler.Api/Validators/RegisterRequestValidator.cs
using FluentValidation;
using MedicineScheduler.Api.DTOs.Auth;

namespace MedicineScheduler.Api.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(72);
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Timezone).NotEmpty();
    }
}

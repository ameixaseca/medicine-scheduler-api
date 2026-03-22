using FluentValidation;
using MedicineScheduler.Api.DTOs.Profile;
using MedicineScheduler.Api.Entities;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Validators;

public class ProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private static readonly string[] ValidPreferences =
        Enum.GetNames<NotificationPreference>().Select(n => n.ToLower()).ToArray();

    public ProfileRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NotificationPreference)
            .Must(p => ValidPreferences.Contains(p.ToLower()))
            .WithMessage($"Must be one of: {string.Join(", ", ValidPreferences)}.");
        RuleFor(x => x.Timezone)
            .NotEmpty()
            .Must(tz => { try { TZConvert.GetTimeZoneInfo(tz); return true; } catch { return false; } })
            .WithMessage("Invalid IANA timezone.");
    }
}

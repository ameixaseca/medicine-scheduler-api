// src/MedicineScheduler.Api/Validators/SettingsValidator.cs
using FluentValidation;
using MedicineScheduler.Api.DTOs.Settings;
using MedicineScheduler.Api.Entities;
using TimeZoneConverter;

namespace MedicineScheduler.Api.Validators;

public class SettingsValidator : AbstractValidator<SettingsDto>
{
    private static readonly string[] ValidPreferences =
        Enum.GetNames<NotificationPreference>().Select(n => n.ToLower()).ToArray();

    public SettingsValidator()
    {
        RuleFor(x => x.NotificationPreference)
            .Must(p => ValidPreferences.Contains(p.ToLower()))
            .WithMessage($"Must be one of: {string.Join(", ", ValidPreferences)}.");
        RuleFor(x => x.Timezone)
            .Must(tz => { try { TZConvert.GetTimeZoneInfo(tz); return true; } catch { return false; } })
            .WithMessage("Invalid IANA timezone.");
    }
}

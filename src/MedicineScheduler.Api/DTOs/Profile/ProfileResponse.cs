namespace MedicineScheduler.Api.DTOs.Profile;

/// <summary>User profile data returned by GET and PUT /profile endpoints.</summary>
public record ProfileResponse(
    string Name,
    string? ProfilePicture,
    string Timezone,
    string NotificationPreference);

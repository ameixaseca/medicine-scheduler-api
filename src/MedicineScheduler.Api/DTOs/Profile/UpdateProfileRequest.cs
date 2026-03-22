namespace MedicineScheduler.Api.DTOs.Profile;

/// <summary>Payload for updating the user profile.</summary>
public record UpdateProfileRequest(string Name, string Timezone, string NotificationPreference);

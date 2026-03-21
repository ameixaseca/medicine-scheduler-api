namespace MedicineScheduler.Api.DTOs.Auth;
public record AuthResponse(string AccessToken, int ExpiresIn = 3600);

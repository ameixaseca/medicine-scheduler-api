namespace MedicineScheduler.Api.DTOs.Auth;
public record RegisterRequest(string Name, string Email, string Password, string Timezone);

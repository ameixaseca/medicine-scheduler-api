namespace MedicineScheduler.Api.DTOs.Push;
public record SubscribeRequest(string Endpoint, string P256dh, string Auth);

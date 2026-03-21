namespace MedicineScheduler.Api.Entities;

public class PushSubscription
{
    public Guid Id { get; set; }
    public string Endpoint { get; set; } = "";
    public string P256dhKey { get; set; } = "";
    public string AuthKey { get; set; } = "";
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

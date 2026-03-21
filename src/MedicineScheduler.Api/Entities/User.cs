namespace MedicineScheduler.Api.Entities;

public enum NotificationPreference { Push, Alarm, Both }

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Timezone { get; set; } = "UTC";
    public NotificationPreference NotificationPreference { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<Patient> Patients { get; set; } = [];
    public ICollection<PushSubscription> PushSubscriptions { get; set; } = [];
}

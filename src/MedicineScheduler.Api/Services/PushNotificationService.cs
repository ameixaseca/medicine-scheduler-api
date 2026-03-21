using MedicineScheduler.Api.Entities;

namespace MedicineScheduler.Api.Services;

public interface IPushNotificationService
{
    Task SendAsync(User user, string title, string body);
}

public class PushNotificationService(IConfiguration config) : IPushNotificationService
{
    public Task SendAsync(User user, string title, string body)
    {
        // Implemented in Task 12
        return Task.CompletedTask;
    }
}

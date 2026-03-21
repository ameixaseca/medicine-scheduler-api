using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.Entities;
using Microsoft.EntityFrameworkCore;
using WebPush;
using System.Text.Json;

namespace MedicineScheduler.Api.Services;

public interface IPushNotificationService
{
    Task SendAsync(User user, string title, string body);
}

public class PushNotificationService(AppDbContext db, IConfiguration config) : IPushNotificationService
{
    public async Task SendAsync(User user, string title, string body)
    {
        var vapidSubject = config["Vapid:Subject"]!;
        var vapidPublicKey = config["Vapid:PublicKey"]!;
        var vapidPrivateKey = config["Vapid:PrivateKey"]!;

        var subs = await db.PushSubscriptions
            .Where(s => s.UserId == user.Id)
            .ToListAsync();

        var payload = JsonSerializer.Serialize(new { title, body });

        foreach (var sub in subs)
        {
            var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dhKey, sub.AuthKey);
            var vapidDetails = new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
            var client = new WebPushClient();
            try
            {
                await client.SendNotificationAsync(pushSub, payload, vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // Subscription expired — remove it
                db.PushSubscriptions.Remove(sub);
            }
        }

        await db.SaveChangesAsync();
    }
}

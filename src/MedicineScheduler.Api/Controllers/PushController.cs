using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Push;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("push")]
[Authorize]
public class PushController(AppDbContext db) : ControllerBase
{
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req)
    {
        var userId = User.GetUserId();
        var existing = await db.PushSubscriptions
            .SingleOrDefaultAsync(s => s.UserId == userId && s.Endpoint == req.Endpoint);

        if (existing != null)
        {
            existing.P256dhKey = req.P256dh;
            existing.AuthKey = req.Auth;
            await db.SaveChangesAsync();
            return Ok();
        }

        db.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Endpoint = req.Endpoint,
            P256dhKey = req.P256dh,
            AuthKey = req.Auth
        });
        await db.SaveChangesAsync();
        return StatusCode(201);
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest req)
    {
        var userId = User.GetUserId();
        var sub = await db.PushSubscriptions
            .SingleOrDefaultAsync(s => s.UserId == userId && s.Endpoint == req.Endpoint);

        if (sub != null)
        {
            db.PushSubscriptions.Remove(sub);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }
}

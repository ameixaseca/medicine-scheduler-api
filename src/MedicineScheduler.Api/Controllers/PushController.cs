using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.Data;
using MedicineScheduler.Api.DTOs.Push;
using MedicineScheduler.Api.Entities;
using MedicineScheduler.Api.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MedicineScheduler.Api.Controllers;

/// <summary>Manage Web Push notification subscriptions.</summary>
[ApiController]
[Route("push")]
[Authorize]
[Produces("application/json")]
public class PushController(AppDbContext db) : ControllerBase
{
    /// <summary>Subscribe a browser endpoint for push notifications.</summary>
    /// <remarks>If a subscription for the same endpoint already exists, its keys are updated.</remarks>
    /// <response code="200">Subscription updated (endpoint already existed).</response>
    /// <response code="201">Subscription created.</response>
    [HttpPost("subscribe")]
    [ProducesResponseType(200)]
    [ProducesResponseType(201)]
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

    /// <summary>Unsubscribe a browser endpoint from push notifications.</summary>
    /// <remarks>Silently succeeds even if the endpoint is not found.</remarks>
    /// <response code="204">Unsubscribed (or endpoint was not registered).</response>
    [HttpPost("unsubscribe")]
    [ProducesResponseType(204)]
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

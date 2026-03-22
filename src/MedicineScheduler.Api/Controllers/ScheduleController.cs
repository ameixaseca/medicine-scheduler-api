using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Schedule;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

/// <summary>View and act on the daily medication schedule.</summary>
[ApiController]
[Route("schedule")]
[Authorize]
[Produces("application/json")]
public class ScheduleController(ScheduleService scheduleService) : ControllerBase
{
    /// <summary>Get today's medication schedule for the authenticated user.</summary>
    /// <remarks>
    /// "Today" is resolved using the user's configured timezone.
    /// All <c>scheduledTime</c> values are in UTC; <c>scheduledTimeLocal</c> is in HH:mm local time.
    /// </remarks>
    /// <response code="200">Returns the schedule items for today.</response>
    [HttpGet("today")]
    [ProducesResponseType(typeof(List<ScheduleItemResponse>), 200)]
    public async Task<IActionResult> Today() =>
        Ok(await scheduleService.GetForTodayAsync(User.GetUserId()));

    /// <summary>Get the medication schedule for a specific date.</summary>
    /// <param name="date">Date in <c>yyyy-MM-dd</c> format (local date in user's timezone).</param>
    /// <response code="200">Returns the schedule items for the given date.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<ScheduleItemResponse>), 200)]
    public async Task<IActionResult> ByDate([FromQuery] DateOnly date) =>
        Ok(await scheduleService.GetForDateAsync(date, User.GetUserId()));

    /// <summary>Mark a medication log as taken.</summary>
    /// <response code="200">Log confirmed.</response>
    /// <response code="404">Log not found.</response>
    [HttpPost("{logId:guid}/confirm")]
    [ProducesResponseType(typeof(LogActionResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Confirm(Guid logId) =>
        Ok(await scheduleService.ConfirmAsync(logId, User.GetUserId()));

    /// <summary>Mark a medication log as skipped by the caregiver.</summary>
    /// <response code="200">Log skipped.</response>
    /// <response code="404">Log not found.</response>
    [HttpPost("{logId:guid}/skip")]
    [ProducesResponseType(typeof(LogActionResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Skip(Guid logId) =>
        Ok(await scheduleService.SkipAsync(logId, User.GetUserId()));
}

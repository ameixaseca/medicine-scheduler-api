using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("schedule")]
[Authorize]
public class ScheduleController(ScheduleService scheduleService) : ControllerBase
{
    [HttpGet("today")]
    public async Task<IActionResult> Today() =>
        Ok(await scheduleService.GetForTodayAsync(User.GetUserId()));

    [HttpGet]
    public async Task<IActionResult> ByDate([FromQuery] DateOnly date) =>
        Ok(await scheduleService.GetForDateAsync(date, User.GetUserId()));

    [HttpPost("{logId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid logId) =>
        Ok(await scheduleService.ConfirmAsync(logId, User.GetUserId()));

    [HttpPost("{logId:guid}/skip")]
    public async Task<IActionResult> Skip(Guid logId) =>
        Ok(await scheduleService.SkipAsync(logId, User.GetUserId()));
}

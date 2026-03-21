using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Medications;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Authorize]
public class MedicationsController(MedicationService medicationService) : ControllerBase
{
    [HttpGet("patients/{patientId:guid}/medications")]
    public async Task<IActionResult> GetAll(Guid patientId) =>
        Ok(await medicationService.GetAllForPatientAsync(patientId, User.GetUserId()));

    [HttpPost("patients/{patientId:guid}/medications")]
    public async Task<IActionResult> Create(Guid patientId, [FromBody] MedicationRequest req)
    {
        var response = await medicationService.CreateAsync(patientId, req, User.GetUserId());
        return StatusCode(201, response);
    }

    [HttpGet("medications/{id:guid}")]
    public async Task<IActionResult> Get(Guid id) =>
        Ok(await medicationService.GetAsync(id, User.GetUserId()));

    [HttpPut("medications/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MedicationRequest req) =>
        Ok(await medicationService.UpdateAsync(id, req, User.GetUserId()));

    [HttpDelete("medications/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await medicationService.DeleteAsync(id, User.GetUserId());
        return NoContent();
    }
}

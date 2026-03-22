using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Medications;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

/// <summary>Manage medications associated with a patient.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public class MedicationsController(MedicationService medicationService) : ControllerBase
{
    /// <summary>List all medications for a patient.</summary>
    /// <response code="200">Returns the list of medications.</response>
    /// <response code="404">Patient not found.</response>
    [HttpGet("patients/{patientId:guid}/medications")]
    [ProducesResponseType(typeof(List<MedicationResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAll(Guid patientId) =>
        Ok(await medicationService.GetAllForPatientAsync(patientId, User.GetUserId()));

    /// <summary>Create a medication for a patient.</summary>
    /// <remarks>
    /// Creates the medication, its initial schedule, a snapshot and generates
    /// the first logs for today and tomorrow based on the patient's timezone.
    /// </remarks>
    /// <response code="201">Medication created.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Patient not found.</response>
    [HttpPost("patients/{patientId:guid}/medications")]
    [ProducesResponseType(typeof(MedicationResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create(Guid patientId, [FromBody] MedicationRequest req)
    {
        var response = await medicationService.CreateAsync(patientId, req, User.GetUserId());
        return StatusCode(201, response);
    }

    /// <summary>Get a medication by ID.</summary>
    /// <response code="200">Returns the medication.</response>
    /// <response code="404">Medication not found.</response>
    [HttpGet("medications/{id:guid}")]
    [ProducesResponseType(typeof(MedicationResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get(Guid id) =>
        Ok(await medicationService.GetAsync(id, User.GetUserId()));

    /// <summary>Update a medication.</summary>
    /// <remarks>
    /// Creates a new schedule snapshot, removes pending future logs and regenerates
    /// them based on the updated schedule.
    /// </remarks>
    /// <response code="200">Medication updated.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Medication not found.</response>
    [HttpPut("medications/{id:guid}")]
    [ProducesResponseType(typeof(MedicationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] MedicationRequest req) =>
        Ok(await medicationService.UpdateAsync(id, req, User.GetUserId()));

    /// <summary>Delete a medication (soft delete).</summary>
    /// <remarks>Also removes all pending future logs for this medication.</remarks>
    /// <response code="204">Medication deleted.</response>
    /// <response code="404">Medication not found.</response>
    [HttpDelete("medications/{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await medicationService.DeleteAsync(id, User.GetUserId());
        return NoContent();
    }
}

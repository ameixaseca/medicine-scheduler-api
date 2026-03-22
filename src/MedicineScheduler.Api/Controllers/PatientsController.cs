using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Patients;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

/// <summary>Manage patients belonging to the authenticated user.</summary>
[ApiController]
[Route("patients")]
[Authorize]
[Produces("application/json")]
public class PatientsController(PatientService patientService) : ControllerBase
{
    /// <summary>List all patients of the authenticated user.</summary>
    /// <response code="200">Returns the list of patients.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<PatientResponse>), 200)]
    public async Task<IActionResult> GetAll() =>
        Ok(await patientService.GetAllAsync(User.GetUserId()));

    /// <summary>Get a patient by ID.</summary>
    /// <response code="200">Returns the patient.</response>
    /// <response code="404">Patient not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get(Guid id) =>
        Ok(await patientService.GetAsync(id, User.GetUserId()));

    /// <summary>Create a new patient.</summary>
    /// <response code="201">Patient created.</response>
    /// <response code="400">Validation error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PatientResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] PatientRequest req)
    {
        var response = await patientService.CreateAsync(req, User.GetUserId());
        return StatusCode(201, response);
    }

    /// <summary>Update an existing patient.</summary>
    /// <response code="200">Patient updated.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Patient not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PatientRequest req) =>
        Ok(await patientService.UpdateAsync(id, req, User.GetUserId()));

    /// <summary>Delete a patient (soft delete).</summary>
    /// <response code="204">Patient deleted.</response>
    /// <response code="404">Patient not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await patientService.DeleteAsync(id, User.GetUserId());
        return NoContent();
    }
}

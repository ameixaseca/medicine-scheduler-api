using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicineScheduler.Api.DTOs.Patients;
using MedicineScheduler.Api.Extensions;
using MedicineScheduler.Api.Services;

namespace MedicineScheduler.Api.Controllers;

[ApiController]
[Route("patients")]
[Authorize]
public class PatientsController(PatientService patientService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await patientService.GetAllAsync(User.GetUserId()));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) =>
        Ok(await patientService.GetAsync(id, User.GetUserId()));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PatientRequest req)
    {
        var response = await patientService.CreateAsync(req, User.GetUserId());
        return StatusCode(201, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PatientRequest req) =>
        Ok(await patientService.UpdateAsync(id, req, User.GetUserId()));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await patientService.DeleteAsync(id, User.GetUserId());
        return NoContent();
    }
}

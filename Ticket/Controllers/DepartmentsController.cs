using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ticket.DTOs.Requests;
using Ticket.Filters;
using Ticket.Interfaces.Services;

namespace Ticket.Controllers;

[Route("departments")]
[ApiController]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] bool includeInactive, CancellationToken ct)
    {
        var departments = await _departmentService.GetAllAsync(includeInactive, ct);
        return Ok(departments);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAsync(int id, CancellationToken ct)
    {
        var department = await _departmentService.GetAsync(id, ct);
        return Ok(department);
    }

    [HttpPost]
    [ApiKeyAuthorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreateAsync([FromBody] DepartmentCreateRequest request, CancellationToken ct)
    {
        var department = await _departmentService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = department.Id }, department);
    }

    [HttpPut("{id:int}")]
    [ApiKeyAuthorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] DepartmentUpdateRequest request, CancellationToken ct)
    {
        var department = await _departmentService.UpdateAsync(id, request, ct);
        return Ok(department);
    }
}

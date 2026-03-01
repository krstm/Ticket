using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ticket.DTOs.Requests;
using Ticket.Filters;
using Ticket.Interfaces.Services;

namespace Ticket.Controllers;

[Route("categories")]
[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] bool includeInactive, CancellationToken ct)
    {
        var categories = await _categoryService.GetAllAsync(includeInactive, ct);
        return Ok(categories);
    }

    [HttpPost]
    [ApiKeyAuthorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreateAsync([FromBody] CategoryCreateRequest request, CancellationToken ct)
    {
        var category = await _categoryService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAllAsync), new { id = category.Id }, category);
    }

    [HttpPut("{id:int}")]
    [ApiKeyAuthorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] CategoryUpdateRequest request, CancellationToken ct)
    {
        var category = await _categoryService.UpdateAsync(id, request, ct);
        return Ok(category);
    }

    [HttpDelete("{id:int}")]
    [ApiKeyAuthorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> DeactivateAsync(int id, CancellationToken ct)
    {
        await _categoryService.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reactivate")]
    [ApiKeyAuthorize]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> ReactivateAsync(int id, CancellationToken ct)
    {
        await _categoryService.ReactivateAsync(id, ct);
        return NoContent();
    }
}

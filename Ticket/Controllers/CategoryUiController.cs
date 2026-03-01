using Microsoft.AspNetCore.Mvc;
using Ticket.DTOs.Requests;
using Ticket.Interfaces.Services;

namespace Ticket.Controllers;

[Route("ui/categories")]
public class CategoryUiController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoryUiController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var categories = await _categoryService.GetAllAsync(includeInactive: true, ct);
        return View(categories);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View();
    }
}

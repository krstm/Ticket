using Microsoft.AspNetCore.Mvc;
using Ticket.Interfaces.Services;

namespace Ticket.Controllers;

[Route("ui/departments")]
public class DepartmentUiController : Controller
{
    private readonly IDepartmentService _departmentService;

    public DepartmentUiController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var departments = await _departmentService.GetAllAsync(includeInactive: true, ct);
        return View(departments);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var department = await _departmentService.GetAsync(id, ct);
        return View(department);
    }
}

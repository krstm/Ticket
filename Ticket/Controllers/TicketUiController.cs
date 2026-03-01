using Microsoft.AspNetCore.Mvc;
using Ticket.DTOs.Requests;
using Ticket.Interfaces.Services;

namespace Ticket.Controllers;

[Route("ui/tickets")]
public class TicketUiController : Controller
{
    private readonly ITicketService _ticketService;
    private readonly ICategoryService _categoryService;
    private readonly IDepartmentService _departmentService;

    public TicketUiController(ITicketService ticketService, ICategoryService categoryService, IDepartmentService departmentService)
    {
        _ticketService = ticketService;
        _categoryService = categoryService;
        _departmentService = departmentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] TicketQueryParameters query, CancellationToken ct)
    {
        query.PageSize = 15; // Standard page size for UI
        var result = await _ticketService.SearchAsync(query, ct);
        ViewBag.Categories = await _categoryService.GetAllAsync(false, ct);
        ViewBag.Departments = await _departmentService.GetAllAsync(includeInactive: false, ct);
        return View(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var ticket = await _ticketService.GetAsync(id, ct);
        return View(ticket);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Categories = await _categoryService.GetAllAsync(false, ct);
        ViewBag.Departments = await _departmentService.GetAllAsync(false, ct);
        return View();
    }
}

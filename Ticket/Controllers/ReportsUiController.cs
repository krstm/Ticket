using Microsoft.AspNetCore.Mvc;
using Ticket.DTOs.Requests;
using Ticket.Interfaces.Services;

namespace Ticket.Controllers;

[Route("ui/reports")]
public class ReportsUiController : Controller
{
    private readonly IReportingService _reportingService;

    public ReportsUiController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ReportQuery query, CancellationToken ct)
    {
        var summary = await _reportingService.GetSummaryAsync(query, ct);
        var trend = await _reportingService.GetStatusTrendAsync(query, ct);
        
        ViewBag.TrendData = trend;
        return View(summary);
    }
}

using Microsoft.AspNetCore.Mvc;
using Ticket.DTOs.Requests;
using Ticket.Interfaces.Services;

namespace Ticket.Controllers;

[Route("reports")]
[ApiController]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;

    public ReportsController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummaryAsync([FromQuery] ReportQuery query, CancellationToken ct)
    {
        var result = await _reportingService.GetSummaryAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("trend")]
    public async Task<IActionResult> GetTrendAsync([FromQuery] ReportQuery query, CancellationToken ct)
    {
        var result = await _reportingService.GetStatusTrendAsync(query, ct);
        return Ok(result);
    }
}

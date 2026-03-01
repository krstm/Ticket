using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Ticket.DTOs.Requests;
using Ticket.Interfaces.Services;

namespace Ticket.Controllers;

[Route("tickets")]
[ApiController]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] TicketQueryParameters query, CancellationToken ct)
    {
        var result = await _ticketService.SearchAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var ticket = await _ticketService.GetAsync(id, ct);
        return Ok(ticket);
    }

    [HttpPost]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreateAsync([FromBody] TicketCreateRequest request, CancellationToken ct)
    {
        var ticket = await _ticketService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = ticket.Id }, ticket);
    }

    [HttpPut("{id:guid}")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] TicketUpdateRequest request, CancellationToken ct)
    {
        var rowVersion = ParseRowVersion();
        var ticket = await _ticketService.UpdateAsync(id, request, rowVersion, ct);
        return Ok(ticket);
    }

    [HttpPatch("{id:guid}/status")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> UpdateStatusAsync(Guid id, [FromBody] TicketStatusUpdateRequest request, CancellationToken ct)
    {
        var rowVersion = ParseRowVersion();
        var ticket = await _ticketService.UpdateStatusAsync(id, request, rowVersion, ct);
        return Ok(ticket);
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> GetCommentsAsync(Guid id, CancellationToken ct)
    {
        var comments = await _ticketService.GetCommentsAsync(id, ct);
        return Ok(comments);
    }

    [HttpPost("{id:guid}/comments")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> AddCommentAsync(Guid id, [FromBody] TicketCommentCreateRequest request, CancellationToken ct)
    {
        var comment = await _ticketService.AddCommentAsync(id, request, ct);
        return Ok(comment);
    }

    private byte[] ParseRowVersion()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.IfMatch, out var values))
        {
            throw new Exceptions.BadRequestException("If-Match header with row version is required.");
        }

        var token = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exceptions.BadRequestException("If-Match header cannot be empty.");
        }

        token = token.Trim('"');
        try
        {
            return Convert.FromBase64String(token);
        }
        catch (FormatException)
        {
            throw new Exceptions.BadRequestException("If-Match header must be a valid base64 row version.");
        }
    }
}

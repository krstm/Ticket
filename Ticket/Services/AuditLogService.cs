using Microsoft.Extensions.Logging;
using TicketEntity = Ticket.Domain.Entities.Ticket;
using Ticket.Interfaces.Services;

namespace Ticket.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(ILogger<AuditLogService> logger)
    {
        _logger = logger;
    }

    public Task RecordAsync(TicketEntity ticket, string action, string? note, string changedBy, CancellationToken ct)
    {
        _logger.LogInformation("Audit: {Action} on ticket {TicketId} by {ChangedBy}. Note: {Note}", action, ticket.Id, changedBy, note);
        return Task.CompletedTask;
    }
}

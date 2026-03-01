using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Interfaces.Services;

public interface IAuditLogService
{
    Task RecordAsync(TicketEntity ticket, string action, string? note, string changedBy, CancellationToken ct);
}

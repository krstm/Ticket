using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Interfaces.Services;

public interface INotificationService
{
    Task NotifyTicketCreatedAsync(TicketEntity ticket, string createdBy, CancellationToken ct);
    Task NotifyTicketResolvedAsync(TicketEntity ticket, string changedBy, string? note, CancellationToken ct);
}

using Ticket.Domain.Entities;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Interfaces.Services;

public interface INotificationService
{
    Task NotifyTicketCreatedAsync(TicketEntity ticket, string createdBy, IReadOnlyCollection<string> recipientEmails, CancellationToken ct);
    Task NotifyTicketResolvedAsync(TicketEntity ticket, string changedBy, string? note, IReadOnlyCollection<string> recipientEmails, CancellationToken ct);
    Task NotifyTicketCommentAddedAsync(TicketEntity ticket, TicketComment comment, IReadOnlyCollection<string> recipientEmails, CancellationToken ct);
}

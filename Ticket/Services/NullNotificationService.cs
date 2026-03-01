using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ticket.Interfaces.Services;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Services;

/// <summary>
/// No-op notification implementation. Real transports (SMTP/webhooks, etc.) will replace this via DI.
/// </summary>
public class NullNotificationService : INotificationService
{
    public Task NotifyTicketCreatedAsync(TicketEntity ticket, string createdBy, IReadOnlyCollection<string> recipientEmails, CancellationToken ct) =>
        Task.CompletedTask;

    public Task NotifyTicketResolvedAsync(TicketEntity ticket, string changedBy, string? note, IReadOnlyCollection<string> recipientEmails, CancellationToken ct) =>
        Task.CompletedTask;

    public Task NotifyTicketCommentAddedAsync(TicketEntity ticket, Domain.Entities.TicketComment comment, IReadOnlyCollection<string> recipientEmails, CancellationToken ct) =>
        Task.CompletedTask;
}

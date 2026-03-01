using MediatR;
using Microsoft.Extensions.Logging;
using Ticket.Domain.Events;

namespace Ticket.Domain.EventHandlers;

public class AuditLogHandler :
    INotificationHandler<TicketCreatedEvent>,
    INotificationHandler<TicketStatusChangedEvent>
{
    private readonly ILogger<AuditLogHandler> _logger;

    public AuditLogHandler(ILogger<AuditLogHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TicketCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Audit: ticket {TicketId} created by {User} with category {CategoryId} and priority {Priority}.",
            notification.Ticket.Id,
            notification.CreatedBy,
            notification.Ticket.CategoryId,
            notification.Ticket.Priority);
        return Task.CompletedTask;
    }

    public Task Handle(TicketStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Audit: ticket {TicketId} status changed from {PreviousStatus} to {NewStatus} by {User}. Note: {Note}",
            notification.Ticket.Id,
            notification.PreviousStatus,
            notification.NewStatus,
            notification.ChangedBy,
            notification.Note);
        return Task.CompletedTask;
    }
}

using MediatR;
using Ticket.Domain.Events;
using Ticket.Interfaces.Services;

namespace Ticket.Domain.EventHandlers;

public class NotificationPlaceholderHandler :
    INotificationHandler<TicketCreatedEvent>,
    INotificationHandler<TicketResolvedEvent>
{
    private readonly INotificationService _notificationService;

    public NotificationPlaceholderHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(TicketCreatedEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.NotifyTicketCreatedAsync(notification.Ticket, notification.CreatedBy, cancellationToken);
    }

    public Task Handle(TicketResolvedEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.NotifyTicketResolvedAsync(notification.Ticket, notification.ChangedBy, notification.Note, cancellationToken);
    }
}

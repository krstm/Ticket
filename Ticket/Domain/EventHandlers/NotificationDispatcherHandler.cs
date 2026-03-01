using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Ticket.Domain.Events;
using Ticket.Interfaces.Services;

namespace Ticket.Domain.EventHandlers;

public class NotificationDispatcherHandler :
    INotificationHandler<TicketCreatedEvent>,
    INotificationHandler<TicketResolvedEvent>,
    INotificationHandler<TicketCommentAddedEvent>
{
    private readonly INotificationService _notificationService;

    public NotificationDispatcherHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(TicketCreatedEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.NotifyTicketCreatedAsync(
            notification.Ticket,
            notification.CreatedBy,
            ResolveRecipients(notification.DepartmentMembers),
            cancellationToken);
    }

    public Task Handle(TicketResolvedEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.NotifyTicketResolvedAsync(
            notification.Ticket,
            notification.ChangedBy,
            notification.Note,
            ResolveRecipients(notification.DepartmentMembers),
            cancellationToken);
    }

    public Task Handle(TicketCommentAddedEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.NotifyTicketCommentAddedAsync(
            notification.Ticket,
            notification.Comment,
            ResolveRecipients(notification.DepartmentMembers),
            cancellationToken);
    }

    private static IReadOnlyCollection<string> ResolveRecipients(IReadOnlyCollection<Domain.Entities.DepartmentMember> members)
    {
        return members
            .Where(m => m.IsActive && m.NotifyOnTicketEmail)
            .Select(m => m.Email)
            .ToArray();
    }
}

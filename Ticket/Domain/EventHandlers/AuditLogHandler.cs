using MediatR;
using Microsoft.Extensions.Logging;
using Ticket.Domain.Events;

namespace Ticket.Domain.EventHandlers;

public class AuditLogHandler :
    INotificationHandler<TicketCreatedEvent>,
    INotificationHandler<TicketStatusChangedEvent>,
    INotificationHandler<TicketCommentAddedEvent>
{
    private readonly ILogger<AuditLogHandler> _logger;

    public AuditLogHandler(ILogger<AuditLogHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TicketCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Audit: ticket {TicketId} created by {User} with category {CategoryId}, department {DepartmentId}, priority {Priority}.",
            notification.Ticket.Id,
            notification.CreatedBy,
            notification.Ticket.CategoryId,
            notification.Department.Id,
            notification.Ticket.Priority);
        return Task.CompletedTask;
    }

    public Task Handle(TicketStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Audit: ticket {TicketId} status changed from {PreviousStatus} to {NewStatus} by {User} ({Email}). Department {DepartmentId}. Note: {Note}",
            notification.Ticket.Id,
            notification.PreviousStatus,
            notification.NewStatus,
            notification.ChangedBy,
            notification.ChangedByEmail,
            notification.Department.Id,
            notification.Note);
        return Task.CompletedTask;
    }

    public Task Handle(TicketCommentAddedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Audit: ticket {TicketId} comment {CommentId} added by {AuthorEmail} ({Source}) in department {DepartmentId}.",
            notification.Ticket.Id,
            notification.Comment.Id,
            notification.AuthorEmail,
            notification.Source,
            notification.Department.Id);
        return Task.CompletedTask;
    }
}

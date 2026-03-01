using MediatR;
using Microsoft.Extensions.Logging;
using Ticket.Data;
using Ticket.Domain.Entities;
using Ticket.Domain.Events;

namespace Ticket.Domain.EventHandlers;

public class TicketHistoryHandler :
    INotificationHandler<TicketCreatedEvent>,
    INotificationHandler<TicketStatusChangedEvent>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TicketHistoryHandler> _logger;

    public TicketHistoryHandler(ApplicationDbContext context, ILogger<TicketHistoryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(TicketCreatedEvent notification, CancellationToken cancellationToken)
    {
        var history = new TicketHistory
        {
            TicketId = notification.Ticket.Id,
            Status = notification.Ticket.Status,
            Action = "Ticket created",
            Note = notification.ReferenceCode,
            ChangedBy = notification.CreatedBy,
            OccurredAtUtc = notification.OccurredOnUtc
        };

        _context.TicketHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("History entry created for ticket {TicketId} (creation).", notification.Ticket.Id);
    }

    public async Task Handle(TicketStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        var history = new TicketHistory
        {
            TicketId = notification.Ticket.Id,
            Status = notification.NewStatus,
            Action = $"Status changed from {notification.PreviousStatus} to {notification.NewStatus}",
            Note = notification.Note,
            ChangedBy = notification.ChangedBy,
            OccurredAtUtc = notification.OccurredOnUtc
        };

        _context.TicketHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("History entry created for ticket {TicketId} (status change).", notification.Ticket.Id);
    }
}

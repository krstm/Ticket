using Ticket.Domain.Enums;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Domain.Events;

public sealed record TicketStatusChangedEvent(
    TicketEntity Ticket,
    TicketStatus PreviousStatus,
    TicketStatus NewStatus,
    string ChangedBy,
    string? Note,
    DateTimeOffset OccurredOnUtc) : DomainEventBase(OccurredOnUtc);

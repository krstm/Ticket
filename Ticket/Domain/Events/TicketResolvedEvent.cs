using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Domain.Events;

public sealed record TicketResolvedEvent(
    TicketEntity Ticket,
    string ChangedBy,
    string? Note,
    DateTimeOffset OccurredOnUtc) : DomainEventBase(OccurredOnUtc);

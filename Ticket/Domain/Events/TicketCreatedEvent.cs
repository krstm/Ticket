using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Domain.Events;

public sealed record TicketCreatedEvent(
    TicketEntity Ticket,
    string CreatedBy,
    string? ReferenceCode,
    DateTimeOffset OccurredOnUtc) : DomainEventBase(OccurredOnUtc);

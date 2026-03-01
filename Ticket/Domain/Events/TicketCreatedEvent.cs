using Ticket.Domain.Entities;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Domain.Events;

public sealed record TicketCreatedEvent(
    TicketEntity Ticket,
    string CreatedBy,
    string? ReferenceCode,
    DateTimeOffset OccurredOnUtc,
    Department Department,
    IReadOnlyCollection<DepartmentMember> DepartmentMembers) : DomainEventBase(OccurredOnUtc);

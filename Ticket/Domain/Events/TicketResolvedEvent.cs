using System;
using System.Collections.Generic;
using Ticket.Domain.Entities;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Domain.Events;

public sealed record TicketResolvedEvent(
    TicketEntity Ticket,
    string ChangedBy,
    string? Note,
    DateTimeOffset OccurredOnUtc,
    string ChangedByEmail,
    Department Department,
    IReadOnlyCollection<DepartmentMember> DepartmentMembers) : DomainEventBase(OccurredOnUtc);

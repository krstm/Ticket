using System;
using System.Collections.Generic;
using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Domain.Events;

public sealed record TicketStatusChangedEvent(
    TicketEntity Ticket,
    TicketStatus PreviousStatus,
    TicketStatus NewStatus,
    string ChangedBy,
    string ChangedByEmail,
    string? Note,
    DateTimeOffset OccurredOnUtc,
    Department Department,
    IReadOnlyCollection<DepartmentMember> DepartmentMembers) : DomainEventBase(OccurredOnUtc);

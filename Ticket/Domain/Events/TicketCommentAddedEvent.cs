using System;
using System.Collections.Generic;
using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Domain.Events;

public sealed record TicketCommentAddedEvent(
    TicketEntity Ticket,
    TicketComment Comment,
    TicketCommentSource Source,
    string AuthorEmail,
    DateTimeOffset OccurredOnUtc,
    Department Department,
    IReadOnlyCollection<DepartmentMember> DepartmentMembers) : DomainEventBase(OccurredOnUtc);

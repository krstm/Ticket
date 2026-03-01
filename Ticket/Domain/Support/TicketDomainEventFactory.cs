using System;
using System.Collections.Generic;
using System.Linq;
using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using Ticket.Domain.Events;
using Ticket.Domain.ValueObjects;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Domain.Support;

public static class TicketDomainEventFactory
{
    public static TicketCreatedEvent Created(
        TicketEntity ticket,
        string createdBy,
        string? referenceCode,
        DateTimeOffset occurredAtUtc,
        Department department) =>
        new(ticket, createdBy, referenceCode, occurredAtUtc, department, FilterActiveMembers(department));

    public static TicketStatusChangedEvent StatusChanged(
        TicketEntity ticket,
        TicketStatus previousStatus,
        TicketStatus newStatus,
        TicketActorContext actor,
        string? note,
        DateTimeOffset occurredAtUtc,
        Department department) =>
        new(ticket, previousStatus, newStatus, actor.DisplayName, actor.Email, note, occurredAtUtc, department, FilterActiveMembers(department));

    public static TicketResolvedEvent Resolved(
        TicketEntity ticket,
        TicketActorContext actor,
        string? note,
        DateTimeOffset occurredAtUtc,
        Department department) =>
        new(ticket, actor.DisplayName, note, occurredAtUtc, actor.Email, department, FilterActiveMembers(department));

    public static TicketCommentAddedEvent CommentAdded(
        TicketEntity ticket,
        TicketComment comment,
        Department department) =>
        new(ticket, comment, comment.Source, comment.AuthorEmail, comment.CreatedAtUtc, department, FilterActiveMembers(department));

    private static IReadOnlyCollection<DepartmentMember> FilterActiveMembers(Department department) =>
        department.Members
            .Where(m => m.IsActive)
            .ToArray();
}

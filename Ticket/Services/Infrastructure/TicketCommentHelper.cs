using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using Ticket.Domain.ValueObjects;
using Ticket.Interfaces.Infrastructure;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Services.Infrastructure;

public static class TicketCommentHelper
{
    public static TicketComment CreateComment(
        TicketEntity ticket,
        TicketActorContext actor,
        string body,
        IContentSanitizer sanitizer,
        IClock clock)
    {
        var sanitized = TicketMutationHelper.SanitizeRequired(sanitizer, body, "Comment body cannot be empty after sanitization.");

        return new TicketComment
        {
            TicketId = ticket.Id,
            Body = sanitized,
            AuthorDisplayName = string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.Email : actor.DisplayName,
            AuthorEmail = actor.Email.Trim(),
            AuthorEmailNormalized = actor.NormalizedEmail,
            Source = actor.ActorType == TicketActorType.Requester ? TicketCommentSource.Requester : TicketCommentSource.DepartmentMember,
            CreatedAtUtc = clock.UtcNow
        };
    }
}

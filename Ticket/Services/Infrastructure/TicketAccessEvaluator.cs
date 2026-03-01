using Ticket.Domain.Enums;
using Ticket.Domain.ValueObjects;
using Ticket.Exceptions;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Services.Infrastructure;

public class TicketAccessEvaluator
{
    public void EnsureParticipant(TicketEntity ticket, TicketActorContext actor)
    {
        if (!IsParticipant(ticket, actor))
        {
            throw new ForbiddenException("Only the requester or assigned department members may modify this ticket.");
        }
    }

    public void EnsureCanModifyDescription(TicketActorContext actor)
    {
        if (actor.ActorType != TicketActorType.Requester)
        {
            throw new ForbiddenException("Only the requester may edit the ticket description.");
        }
    }

    public bool IsParticipant(TicketEntity ticket, TicketActorContext actor)
    {
        return actor.ActorType switch
        {
            TicketActorType.Requester => MatchesRequester(ticket, actor.NormalizedEmail),
            TicketActorType.DepartmentMember => MatchesDepartmentMember(ticket, actor.NormalizedEmail),
            _ => false
        };
    }

    private static bool MatchesRequester(TicketEntity ticket, string normalizedEmail)
    {
        if (string.IsNullOrWhiteSpace(ticket.Requester.Email))
        {
            return false;
        }

        return string.Equals(ticket.Requester.Email.Trim().ToUpperInvariant(), normalizedEmail, StringComparison.Ordinal);
    }

    private static bool MatchesDepartmentMember(TicketEntity ticket, string normalizedEmail)
    {
        return ticket.Department?.Members.Any(m => m.IsActive && m.EmailNormalized == normalizedEmail) ?? false;
    }
}

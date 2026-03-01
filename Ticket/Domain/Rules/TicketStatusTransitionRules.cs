using Ticket.Domain.Enums;

namespace Ticket.Domain.Rules;

public static class TicketStatusTransitionRules
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> Matrix = new Dictionary<TicketStatus, TicketStatus[]>
    {
        { TicketStatus.New, new[] { TicketStatus.InProgress, TicketStatus.Rejected } },
        { TicketStatus.InProgress, new[] { TicketStatus.AwaitingResponse, TicketStatus.Resolved, TicketStatus.Rejected } },
        { TicketStatus.AwaitingResponse, new[] { TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Rejected } },
        { TicketStatus.Resolved, new[] { TicketStatus.Closed, TicketStatus.Rejected } },
        { TicketStatus.Closed, Array.Empty<TicketStatus>() },
        { TicketStatus.Rejected, Array.Empty<TicketStatus>() }
    };

    public static bool CanTransition(TicketStatus from, TicketStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return Matrix.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}

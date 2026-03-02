using System.Linq;
using FluentAssertions;
using Ticket.Domain.Enums;
using Ticket.Domain.Rules;

namespace Ticket.Tests.Unit.Domain;

public class TicketStatusTransitionRulesTests
{
    public static IEnumerable<object[]> TransitionMatrix =>
        from fromStatus in Enum.GetValues<TicketStatus>()
        from toStatus in Enum.GetValues<TicketStatus>()
        let expected = Expected(fromStatus, toStatus)
        select new object[] { fromStatus, toStatus, expected };

    [Theory]
    [MemberData(nameof(TransitionMatrix))]
    public void CanTransition_ShouldFollowDefinedMatrix(TicketStatus from, TicketStatus to, bool expected)
    {
        var result = TicketStatusTransitionRules.CanTransition(from, to);
        result.Should().Be(expected, $"{from} -> {to} should {(expected ? "" : "not ")}be allowed");
    }

    private static bool Expected(TicketStatus from, TicketStatus to)
    {
        if (from == to)
        {
            return true;
        }

        var matrix = new Dictionary<TicketStatus, TicketStatus[]>
        {
            { TicketStatus.New, new[] { TicketStatus.InProgress, TicketStatus.Rejected } },
            { TicketStatus.InProgress, new[] { TicketStatus.AwaitingResponse, TicketStatus.Resolved, TicketStatus.Rejected } },
            { TicketStatus.AwaitingResponse, new[] { TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Rejected } },
            { TicketStatus.Resolved, new[] { TicketStatus.Closed, TicketStatus.Rejected } },
            { TicketStatus.Closed, Array.Empty<TicketStatus>() },
            { TicketStatus.Rejected, Array.Empty<TicketStatus>() }
        };

        return matrix.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}

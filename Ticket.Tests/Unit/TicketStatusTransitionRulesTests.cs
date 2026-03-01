using Ticket.Domain.Enums;
using Ticket.Domain.Rules;

namespace Ticket.Tests.Unit;

public class TicketStatusTransitionRulesTests
{
    [Theory]
    [InlineData(TicketStatus.New, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.New, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress, false)]
    public void CanTransition_ShouldRespectMatrix(TicketStatus from, TicketStatus to, bool expected)
    {
        var result = TicketStatusTransitionRules.CanTransition(from, to);
        Assert.Equal(expected, result);
    }
}

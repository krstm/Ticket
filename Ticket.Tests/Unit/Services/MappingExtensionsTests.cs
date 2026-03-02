using System.Collections.Generic;
using FluentAssertions;
using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using Ticket.Services.Mapping;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Unit.Services;

public class MappingExtensionsTests
{
    [Fact]
    public void ToDetails_Should_OrderHistoryDescending()
    {
        var ticket = new TicketBuilder().BuildEntity();
        ticket.History = new List<TicketHistory>
        {
            new() { Id = 1, OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10), Status = TicketStatus.New, Action = "Created" },
            new() { Id = 2, OccurredAtUtc = DateTimeOffset.UtcNow, Status = TicketStatus.InProgress, Action = "Progressed" }
        };

        var dto = ticket.ToDetails();
        dto.History.Should().BeInDescendingOrder(h => h.OccurredAtUtc);
    }

    [Fact]
    public void ToDetails_Should_OrderCommentsDescending()
    {
        var ticket = new TicketBuilder().BuildEntity();
        ticket.Comments = new List<TicketComment>
        {
            new() { Id = 1, Body = "first", CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5) },
            new() { Id = 2, Body = "second", CreatedAtUtc = DateTimeOffset.UtcNow }
        };

        var dto = ticket.ToDetails();
        dto.Comments.Should().BeInDescendingOrder(c => c.CreatedAtUtc);
    }

    [Fact]
    public void ToDetails_Should_MapDepartmentFallback()
    {
        var ticket = new TicketBuilder().WithDepartment(42, "Ops").BuildEntity();
        ticket.Department = null;

        var dto = ticket.ToDetails();
        dto.Department.Should().NotBeNull();
        dto.Department.Id.Should().Be(ticket.DepartmentId);
        dto.Department.Name.Should().BeEmpty();
    }

    [Fact]
    public void ToSummary_Should_CopyReferenceCodeAndRowVersion()
    {
        var rowVersion = new byte[] { 1, 2, 3 };
        var ticket = new TicketBuilder()
            .WithReferenceCode("ref")
            .WithRowVersion(rowVersion)
            .BuildEntity();

        var summary = ticket.ToSummary();
        summary.ReferenceCode.Should().Be("ref");
        summary.RowVersion.Should().BeSameAs(rowVersion);
    }
}

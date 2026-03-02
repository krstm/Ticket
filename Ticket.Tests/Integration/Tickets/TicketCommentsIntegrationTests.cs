using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Tests.TestUtilities;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Integration.Tickets;

public class TicketCommentsIntegrationTests : IntegrationTestBase
{
    public TicketCommentsIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Comments_Should_BeReturnedInDescendingOrder()
    {
        var category = await SeedCategoryAsync();
        var department = await SeedDepartmentAsync();
        var tickets = await SeedTicketsAsync(category, department, 1);
        var ticket = tickets.Single();
        var actor = new TicketActorBuilder().AsDepartmentMember().WithEmail("ops.agent@example.com").WithName("Ops").BuildDto();

        await AddComment(ticket.Id, actor, "<b>First</b>");
        Clock.Advance(TimeSpan.FromMinutes(1));
        await AddComment(ticket.Id, actor, "Second comment");

        using var response = await Client.GetAsync($"/tickets/{ticket.Id}/comments");
        var comments = await DeserializeAsync<List<TicketCommentDto>>(response);
        comments.Should().HaveCount(2);
        comments.Should().BeInDescendingOrder(c => c.CreatedAtUtc);
        comments.First().Body.Should().Contain("Second");
    }

    [Fact]
    public async Task AddComment_Should_SanitizeAndTriggerNotification()
    {
        var category = await SeedCategoryAsync();
        var department = await SeedDepartmentAsync();
        var tickets = await SeedTicketsAsync(category, department, 1);
        var ticket = tickets.Single();
        var actor = new TicketActorBuilder().AsDepartmentMember().WithEmail("ops.agent@example.com").WithName("Ops").BuildDto();

        var result = await AddComment(ticket.Id, actor, "<script>alert('x')</script>Clean");

        result.Body.Should().NotContain("<script>");
        NotificationSpy.CommentEvents.Should().HaveCount(1);
        NotificationSpy.CommentEvents.Single().TicketId.Should().Be(ticket.Id);
    }

    private async Task<TicketCommentDto> AddComment(Guid ticketId, TicketActorContextDto actor, string body)
    {
        using var response = await Client.PostAsync($"/tickets/{ticketId}/comments", AsJson(new TicketCommentCreateRequest
        {
            Actor = actor,
            Body = body
        }));

        return await DeserializeAsync<TicketCommentDto>(response);
    }
}

using System.Linq;
using FluentAssertions;
using Ticket.Domain.Enums;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Tests.TestUtilities;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Integration.Tickets;

public class TicketKeysetPaginationTests : IntegrationTestBase
{
    public TicketKeysetPaginationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task KeysetPagination_Should_ReturnOpaqueToken_WithoutDuplicates()
    {
        var category = await SeedCategoryAsync();
        var department = await SeedDepartmentAsync();

        await SeedTicketsAsync(category, department, 5, (index, builder) =>
            builder.WithTitle($"Paged Ticket {index}").WithCreatedAt(Clock.UtcNow.AddMinutes(-index)));

        using var firstResponse = await Client.GetAsync("/tickets?PageSize=2&SortDirection=Desc");
        var firstPage = await DeserializeAsync<PagedResult<TicketSummaryDto>>(firstResponse);

        firstPage.Items.Should().HaveCount(2);
        firstPage.NextPageToken.Should().NotBeNullOrWhiteSpace();

        using var secondResponse = await Client.GetAsync($"/tickets?PageToken={firstPage.NextPageToken}&PageSize=2&SortDirection=Desc");
        var secondPage = await DeserializeAsync<PagedResult<TicketSummaryDto>>(secondResponse);

        secondPage.Items.Should().HaveCount(2);
        secondPage.Items.Select(t => t.Id).Should().NotIntersectWith(firstPage.Items.Select(t => t.Id));
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Resolved")]
    public async Task Search_Should_FilterByStatus(string status)
    {
        var category = await SeedCategoryAsync(c => c.WithName("Ops"));
        var department = await SeedDepartmentAsync(d => d.WithName("Dept"));
        var tickets = await SeedTicketsAsync(category, department, 3, (i, builder) =>
            builder.WithTitle($"Status Ticket {i}").WithDescription("status filter"));

        var targetId = tickets.First().Id;
        var actor = new TicketActorBuilder().AsDepartmentMember().WithEmail("ops.agent@example.com").WithName("Ops").BuildDto();
        var targetStatus = Enum.Parse<TicketStatus>(status);
        var currentRowVersion = tickets.First().RowVersion;

        if (targetStatus == TicketStatus.Resolved)
        {
            currentRowVersion = await UpdateStatusAsync(targetId, currentRowVersion, TicketStatus.InProgress, actor);
        }

        if (targetStatus != TicketStatus.New)
        {
            await UpdateStatusAsync(targetId, currentRowVersion, targetStatus, actor);
        }

        using var listResponse = await Client.GetAsync($"/tickets?Statuses={status}");
        var list = await DeserializeAsync<PagedResult<TicketSummaryDto>>(listResponse);
        list.Items.Should().OnlyContain(t => t.Status.ToString() == status);
    }

    private async Task<byte[]> UpdateStatusAsync(Guid ticketId, byte[] rowVersion, TicketStatus status, TicketActorContextDto actor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/tickets/{ticketId}/status")
        {
            Content = AsJson(new TicketStatusUpdateRequest
            {
                Status = status,
                Actor = actor,
                ChangedBy = actor.Name
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(rowVersion));
        var response = await Client.SendAsync(request);
        var updated = await DeserializeAsync<TicketDetailsDto>(response);
        return updated.RowVersion;
    }
}

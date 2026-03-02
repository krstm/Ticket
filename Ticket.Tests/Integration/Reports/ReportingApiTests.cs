using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Ticket.Domain.Enums;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Tests.TestUtilities;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Integration.Reports;

public class ReportingApiTests : IntegrationTestBase
{
    public ReportingApiTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SummaryReport_ShouldGroupByDepartment()
    {
        var category = await SeedCategoryAsync();
        var deptA = await SeedDepartmentAsync(d => d.WithName("DeptA"));
        var deptB = await SeedDepartmentAsync(d => d.WithName("DeptB"));

        await SeedTicketsAsync(category, deptA, 2);
        await SeedTicketsAsync(category, deptB, 1);

        using var response = await Client.GetAsync("/reports/summary?GroupBy=Department");
        var buckets = await DeserializeAsync<List<ReportBucketDto>>(response);

        buckets.Should().Contain(b => b.Bucket == deptA.Name && b.Count == 2);
        buckets.Should().Contain(b => b.Bucket == deptB.Name && b.Count == 1);
    }

    [Theory]
    [InlineData(ReportInterval.Day)]
    [InlineData(ReportInterval.Week)]
    public async Task TrendReport_ShouldReturnBuckets_ForInterval(ReportInterval interval)
    {
        var category = await SeedCategoryAsync();
        var department = await SeedDepartmentAsync();
        var tickets = await SeedTicketsAsync(category, department, 2);

        var actor = new TicketActorBuilder().AsDepartmentMember().WithEmail("ops.agent@example.com").BuildDto();
        foreach (var ticket in tickets)
        {
            await AdvanceStatus(ticket.Id, ticket.RowVersion, TicketStatus.Resolved, actor);
        }

        var from = Clock.UtcNow.AddDays(-2);
        var to = Clock.UtcNow.AddDays(2);
        var fromValue = Uri.EscapeDataString(from.ToString("O"));
        var toValue = Uri.EscapeDataString(to.ToString("O"));
        var query = $"/reports/trend?Interval={interval}&From={fromValue}&To={toValue}";
        using var response = await Client.GetAsync(query);
        var buckets = await DeserializeAsync<List<ReportBucketDto>>(response);

        buckets.Should().NotBeEmpty();
        buckets.Sum(b => b.Count).Should().BeGreaterOrEqualTo(2);
    }

    private async Task AdvanceStatus(Guid ticketId, byte[] rowVersion, TicketStatus status, TicketActorContextDto actor)
    {
        if (status == TicketStatus.New)
        {
            return;
        }

        var current = rowVersion;
        if (status != TicketStatus.InProgress)
        {
            current = await UpdateStatusAsync(ticketId, current, TicketStatus.InProgress, actor);
        }

        await UpdateStatusAsync(ticketId, current, status, actor);
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
        using var response = await Client.SendAsync(request);
        var updated = await DeserializeAsync<TicketDetailsDto>(response);
        return updated.RowVersion;
    }
}

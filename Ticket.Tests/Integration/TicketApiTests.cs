using System.Net;
using System.Net.Http;
using System.Text.Json;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Domain.Enums;
using Ticket.Tests.TestUtilities;

namespace Ticket.Tests.Integration;

public class TicketApiTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TicketApiTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TicketLifecycle_Should_Create_Update_Status_And_Filter()
    {
        var category = await CreateCategoryAsync("Support");

        var createResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Email not working",
            Description = "<script>alert('x')</script> cannot send email",
            CategoryId = category.Id,
            Priority = TicketPriority.High,
            Requester = new()
            {
                Name = "Jane Doe",
                Email = "jane@example.com"
            }
        }));
        await EnsureSuccessAsync(createResponse);
        var created = await DeserializeAsync<TicketDetailsDto>(createResponse);

        Assert.Single(NotificationSpy.CreatedEvents);

        var updateRequest = new TicketUpdateRequest
        {
            Title = "Email down",
            Description = "Updated description",
            CategoryId = category.Id,
            Priority = TicketPriority.Critical
        };

        var updateMessage = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{created.Id}")
        {
            Content = AsJson(updateRequest)
        };
        updateMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(created.RowVersion));
        var updateResponse = await Client.SendAsync(updateMessage);
        await EnsureSuccessAsync(updateResponse);
        var updated = await DeserializeAsync<TicketDetailsDto>(updateResponse);

        var progressMessage = new HttpRequestMessage(HttpMethod.Patch, $"/tickets/{created.Id}/status")
        {
            Content = AsJson(new TicketStatusUpdateRequest
            {
                Status = TicketStatus.InProgress,
                ChangedBy = "tester"
            })
        };
        progressMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(updated.RowVersion));
        var progressResponse = await Client.SendAsync(progressMessage);
        await EnsureSuccessAsync(progressResponse);
        var progressed = await DeserializeAsync<TicketDetailsDto>(progressResponse);

        var statusMessage = new HttpRequestMessage(HttpMethod.Patch, $"/tickets/{created.Id}/status")
        {
            Content = AsJson(new TicketStatusUpdateRequest
            {
                Status = TicketStatus.Resolved,
                ChangedBy = "tester",
                Note = "Issue fixed"
            })
        };
        statusMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(progressed.RowVersion));
        var statusResponse = await Client.SendAsync(statusMessage);
        await EnsureSuccessAsync(statusResponse);

        Assert.Single(NotificationSpy.ResolvedEvents);

        var listResponse = await Client.GetAsync("/tickets?Statuses=Resolved&SearchTerm=email");
        await EnsureSuccessAsync(listResponse);
        var list = await DeserializeAsync<PagedResult<TicketSummaryDto>>(listResponse);

        Assert.Single(list.Items);
        Assert.Equal(TicketStatus.Resolved, list.Items.First().Status);
        Assert.True(list.TotalCount >= 1);
    }

    [Fact]
    public async Task Concurrency_Should_Return409_On_Stale_RowVersion()
    {
        var category = await CreateCategoryAsync("Networking");
        var createResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "VPN outage",
            Description = "vpn down",
            CategoryId = category.Id
        }));
        await EnsureSuccessAsync(createResponse);
        var created = await DeserializeAsync<TicketDetailsDto>(createResponse);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{created.Id}")
        {
            Content = AsJson(new TicketUpdateRequest
            {
                Title = "VPN outage - edit",
                Description = "desc",
                CategoryId = category.Id
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(created.RowVersion));
        var response1 = await Client.SendAsync(request);
        await EnsureSuccessAsync(response1);

        var staleRequest = new HttpRequestMessage(HttpMethod.Put, $"/tickets/{created.Id}")
        {
            Content = AsJson(new TicketUpdateRequest
            {
                Title = "Stale update",
                Description = "desc",
                CategoryId = category.Id
            })
        };
        staleRequest.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(created.RowVersion));
        var staleResponse = await Client.SendAsync(staleRequest);

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task Reports_ShouldSummarizeTickets()
    {
        var categoryA = await CreateCategoryAsync("HR");
        var categoryB = await CreateCategoryAsync("Finance");

        await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Access form",
            Description = "Need form",
            CategoryId = categoryA.Id,
            Priority = TicketPriority.Low
        }));

        await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Budget tool issue",
            Description = "Cannot open tool",
            CategoryId = categoryB.Id,
            Priority = TicketPriority.High
        }));

        var response = await Client.GetAsync("/reports/summary?groupBy=category");
        response.EnsureSuccessStatusCode();
        var summaries = await DeserializeAsync<List<ReportBucketDto>>(response);

        Assert.Contains(summaries, s => s.Bucket == categoryA.Name && s.Count == 1);
        Assert.Contains(summaries, s => s.Bucket == categoryB.Name && s.Count == 1);
    }

    [Fact]
    public async Task KeysetPagination_Should_ProduceStableSlices()
    {
        var category = await CreateCategoryAsync("Infra");
        for (var i = 0; i < 5; i++)
        {
            await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
            {
                Title = $"Batch {i}",
                Description = "body",
                CategoryId = category.Id
            }));
        }

        var firstPageResponse = await Client.GetAsync("/tickets?PageSize=2&SortDirection=Desc&SortBy=CreatedAt");
        await EnsureSuccessAsync(firstPageResponse);
        var firstPage = await DeserializeAsync<PagedResult<TicketSummaryDto>>(firstPageResponse);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextPageToken));

        var secondPageResponse = await Client.GetAsync($"/tickets?PageSize=2&SortDirection=Desc&SortBy=CreatedAt&PageToken={Uri.EscapeDataString(firstPage.NextPageToken!)}");
        await EnsureSuccessAsync(secondPageResponse);
        var secondPage = await DeserializeAsync<PagedResult<TicketSummaryDto>>(secondPageResponse);

        Assert.Equal(2, secondPage.Items.Count);
        Assert.Empty(firstPage.Items.Select(t => t.Id).Intersect(secondPage.Items.Select(t => t.Id)));
    }

    [Fact]
    public async Task SearchScope_TitleOnly_ShouldRestrictMatches()
    {
        var category = await CreateCategoryAsync("Apps");
        await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Printer",
            Description = "Email outage",
            CategoryId = category.Id
        }));

        var descriptionSearch = await Client.GetAsync("/tickets?SearchTerm=email&SearchScope=FullContent");
        await EnsureSuccessAsync(descriptionSearch);
        var full = await DeserializeAsync<PagedResult<TicketSummaryDto>>(descriptionSearch);
        Assert.Single(full.Items);

        var titleOnly = await Client.GetAsync("/tickets?SearchTerm=email&SearchScope=TitleOnly");
        await EnsureSuccessAsync(titleOnly);
        var scoped = await DeserializeAsync<PagedResult<TicketSummaryDto>>(titleOnly);
        Assert.Empty(scoped.Items);
    }

    [Fact]
    public async Task DomainEvents_ShouldPersistHistory_And_FireNotifications()
    {
        var category = await CreateCategoryAsync("Ops");
        var createResponse = await Client.PostAsync("/tickets", AsJson(new TicketCreateRequest
        {
            Title = "Server issue",
            Description = "desc",
            CategoryId = category.Id
        }));
        await EnsureSuccessAsync(createResponse);
        var created = await DeserializeAsync<TicketDetailsDto>(createResponse);

        var detailsResponse = await Client.GetAsync($"/tickets/{created.Id}");
        await EnsureSuccessAsync(detailsResponse);
        var details = await DeserializeAsync<TicketDetailsDto>(detailsResponse);
        Assert.Contains(details.History, h => h.Action.Contains("Ticket created", StringComparison.OrdinalIgnoreCase));
        Assert.Single(NotificationSpy.CreatedEvents);

        var prepareMessage = new HttpRequestMessage(HttpMethod.Patch, $"/tickets/{created.Id}/status")
        {
            Content = AsJson(new TicketStatusUpdateRequest
            {
                Status = TicketStatus.InProgress,
                ChangedBy = "ops"
            })
        };
        prepareMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(created.RowVersion));
        var prepareResponse = await Client.SendAsync(prepareMessage);
        await EnsureSuccessAsync(prepareResponse);
        var prepared = await DeserializeAsync<TicketDetailsDto>(prepareResponse);

        var statusMessage = new HttpRequestMessage(HttpMethod.Patch, $"/tickets/{created.Id}/status")
        {
            Content = AsJson(new TicketStatusUpdateRequest
            {
                Status = TicketStatus.Resolved,
                ChangedBy = "ops"
            })
        };
        statusMessage.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(prepared.RowVersion));
        await EnsureSuccessAsync(await Client.SendAsync(statusMessage));

        Assert.Single(NotificationSpy.ResolvedEvents);
        var resolvedDetailsResponse = await Client.GetAsync($"/tickets/{created.Id}");
        await EnsureSuccessAsync(resolvedDetailsResponse);
        var resolvedDetails = await DeserializeAsync<TicketDetailsDto>(resolvedDetailsResponse);
        Assert.Contains(resolvedDetails.History, h => h.Action.Contains("Status changed", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<CategoryDto> CreateCategoryAsync(string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/categories")
        {
            Content = AsJson(new CategoryCreateRequest
            {
                Name = name
            })
        };
        request.Headers.Add("X-API-Key", "integration-key");
        var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create category. Status: {response.StatusCode}, Body: {body}");
        }
        return await DeserializeAsync<CategoryDto>(response);
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        return result!;
    }
}

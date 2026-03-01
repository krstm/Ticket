using System.Net;
using System.Net.Http;
using System.Text.Json;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Domain.Enums;
using Ticket.Tests.TestUtilities;
using Xunit.Abstractions;

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

        var listResponse = await Client.GetAsync("/tickets?Statuses=Resolved&SearchTerm=email");
        await EnsureSuccessAsync(listResponse);
        var list = await DeserializeAsync<PagedResult<TicketSummaryDto>>(listResponse);

        Assert.Single(list.Items);
        Assert.Equal(TicketStatus.Resolved, list.Items.First().Status);
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

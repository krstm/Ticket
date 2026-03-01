using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Domain.Enums;
using Ticket.Tests.TestUtilities;

namespace Ticket.Tests.Security;

public class SecurityHardeningTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task CategoryEndpoints_ShouldRequireApiKey()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/categories", IntegrationTestBase.AsJson(new CategoryCreateRequest
        {
            Name = "Finance"
        }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TicketCreation_ShouldSanitizeDescription()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client, "Security");

        var response = await client.PostAsync("/tickets", IntegrationTestBase.AsJson(new TicketCreateRequest
        {
            Title = "XSS attempt",
            Description = "<script>alert('boom')</script> body",
            CategoryId = category.Id,
            Priority = TicketPriority.Medium
        }));

        response.EnsureSuccessStatusCode();
        var ticket = await DeserializeAsync<TicketDetailsDto>(response);

        Assert.DoesNotContain("<script>", ticket.Description, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("<script>alert('boom')</script> body", ticket.Description, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RateLimiter_ShouldThrottleMutations()
    {
        using var factory = CustomWebApplicationFactory.Create(rateLimit: 2);
        var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client, "Ops");

        var tickets = Enumerable.Range(0, 5).Select(i =>
        {
            var message = new HttpRequestMessage(HttpMethod.Post, "/tickets")
            {
                Content = IntegrationTestBase.AsJson(new TicketCreateRequest
                {
                    Title = $"Req {i}",
                    Description = "body",
                    CategoryId = category.Id
                })
            };
            return client.SendAsync(message);
        }).ToArray();

        var responses = await Task.WhenAll(tickets);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task SqlInjectionLikeTerm_ShouldNotCrash()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client, "DBA");

        await client.PostAsync("/tickets", IntegrationTestBase.AsJson(new TicketCreateRequest
        {
            Title = "Database timeout",
            Description = "desc",
            CategoryId = category.Id
        }));

        var response = await client.GetAsync("/tickets?SearchTerm=' OR 1=1;--");
        response.EnsureSuccessStatusCode();
    }

    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient client, string name)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/categories")
        {
            Content = IntegrationTestBase.AsJson(new CategoryCreateRequest
            {
                Name = name
            })
        };
        message.Headers.Add("X-API-Key", "integration-key");
        var response = await client.SendAsync(message);
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

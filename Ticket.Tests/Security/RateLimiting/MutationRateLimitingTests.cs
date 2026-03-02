using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Tests.TestUtilities;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Security.RateLimiting;

public class MutationRateLimitingTests
{
    [Fact]
    public async Task Mutations_Should_BeThrottled()
    {
        using var factory = CustomWebApplicationFactory.Create(rateLimit: 6);
        var client = factory.CreateClient();

        var category = await CreateCategoryAsync(client);
        var department = await CreateDepartmentAsync(client);

        var tasks = Enumerable.Range(0, 10).Select(i => client.PostAsync("/tickets", IntegrationTestBase.AsJson(new TicketBuilder()
            .WithCategory(category.Id, category.Name)
            .WithDepartment(department.Id, department.Name)
            .WithTitle($"RL Ticket {i}")
            .BuildCreateRequest())));

        var results = await Task.WhenAll(tasks);
        results.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Reads_Should_RespectLimiter()
    {
        using var factory = CustomWebApplicationFactory.Create(rateLimit: 2);
        var client = factory.CreateClient();
        await client.GetAsync("/tickets"); // warm up

        var results = await Task.WhenAll(
            client.GetAsync("/tickets"),
            client.GetAsync("/tickets"),
            client.GetAsync("/tickets"));

        results.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient client)
    {
        var builder = new CategoryBuilder();
        using var response = await client.PostAsync("/categories", IntegrationTestBase.AsJson(builder.BuildCreateRequest()));
        return await IntegrationTestBase.DeserializeAsync<CategoryDto>(response);
    }

    private static async Task<DepartmentDto> CreateDepartmentAsync(HttpClient client)
    {
        var builder = new DepartmentBuilder();
        using var response = await client.PostAsync("/departments", IntegrationTestBase.AsJson(builder.BuildCreateRequest()));
        return await IntegrationTestBase.DeserializeAsync<DepartmentDto>(response);
    }
}

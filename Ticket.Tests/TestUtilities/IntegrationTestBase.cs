using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.TestUtilities;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    protected TestNotificationService NotificationSpy => Factory.Services.GetRequiredService<TestNotificationService>();
    protected FakeClock Clock => Factory.Clock;

    protected internal static StringContent AsJson(object payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    protected static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Request failed with status {(int)response.StatusCode}: {body}");
        }
    }

    protected Task ResetNotificationsAsync()
    {
        NotificationSpy.Reset();
        return Task.CompletedTask;
    }

    protected async Task<CategoryDto> SeedCategoryAsync(Action<CategoryBuilder>? configure = null)
    {
        var builder = new CategoryBuilder();
        configure?.Invoke(builder);

        using var response = await Client.PostAsync("/categories", AsJson(builder.BuildCreateRequest()));
        await EnsureSuccessAsync(response);
        return await DeserializeAsync<CategoryDto>(response);
    }

    protected async Task<DepartmentDto> SeedDepartmentAsync(Action<DepartmentBuilder>? configure = null)
    {
        var builder = new DepartmentBuilder();
        configure?.Invoke(builder);

        using var response = await Client.PostAsync("/departments", AsJson(builder.BuildCreateRequest()));
        await EnsureSuccessAsync(response);
        return await DeserializeAsync<DepartmentDto>(response);
    }

    protected async Task<IReadOnlyList<TicketDetailsDto>> SeedTicketsAsync(
        CategoryDto category,
        DepartmentDto department,
        int count,
        Func<int, TicketBuilder, TicketBuilder>? configure = null)
    {
        var results = new List<TicketDetailsDto>(count);

        for (var i = 0; i < count; i++)
        {
            var builder = new TicketBuilder()
                .WithTitle($"Seed Ticket #{i + 1}")
                .WithDescription($"Seed description #{i + 1}")
                .WithCategory(category.Id, category.Name)
                .WithDepartment(department.Id, department.Name)
                .WithReferenceCode($"REF-{i + 1:000}");

            builder = configure?.Invoke(i, builder) ?? builder;

            using var response = await Client.PostAsync("/tickets", AsJson(builder.BuildCreateRequest()));
            await EnsureSuccessAsync(response);
            results.Add(await DeserializeAsync<TicketDetailsDto>(response));
        }

        return results;
    }

    protected internal static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        if (result == null)
        {
            throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");
        }

        return result;
    }

    protected static T LoadJsonFixture<T>(string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Fixture not found at {fullPath}");
        }

        var json = File.ReadAllText(fullPath);
        var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (result == null)
        {
            throw new InvalidOperationException($"Fixture {relativePath} could not be parsed into {typeof(T).Name}.");
        }

        return result;
    }

    public async Task InitializeAsync()
    {
        await Factory.ResetStateAsync();
        await ResetNotificationsAsync();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

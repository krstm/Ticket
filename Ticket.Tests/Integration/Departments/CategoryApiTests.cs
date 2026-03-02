using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Tests.TestUtilities;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Integration.Departments;

public class CategoryApiTests : IntegrationTestBase
{
    public CategoryApiTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateCategory_ShouldRejectDuplicateNames()
    {
        var builder = new CategoryBuilder().WithName("Compliance");
        await SeedCategoryAsync(c => c.WithName("Compliance"));

        using var response = await Client.PostAsync("/categories", AsJson(builder.BuildCreateRequest()));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deactivate_ShouldFail_WhenTicketsExist()
    {
        var category = await SeedCategoryAsync();
        var department = await SeedDepartmentAsync();
        await SeedTicketsAsync(category, department, 1);

        using var response = await Client.DeleteAsync($"/categories/{category.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reactivate_ShouldEnableInactiveCategory()
    {
        var category = await SeedCategoryAsync();

        using (var deactivate = await Client.DeleteAsync($"/categories/{category.Id}"))
        {
            deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using (var reactivate = await Client.PostAsync($"/categories/{category.Id}/reactivate", null))
        {
            reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var getResponse = await Client.GetAsync("/categories?includeInactive=false");
        var categories = await DeserializeAsync<List<CategoryDto>>(getResponse);
        categories.Should().Contain(c => c.Id == category.Id && c.IsActive);
    }
}

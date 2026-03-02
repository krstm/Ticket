using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Tests.TestUtilities;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Integration.Departments;

public class DepartmentApiTests : IntegrationTestBase
{
    public DepartmentApiTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateDepartment_ShouldPersistMembers()
    {
        var builder = new DepartmentBuilder()
            .WithoutMembers()
            .WithMember("Alice Agent", "alice@example.com")
            .WithMember("Bob Agent", "bob@example.com");

        using var response = await Client.PostAsync("/departments", AsJson(builder.BuildCreateRequest()));
        var department = await DeserializeAsync<DepartmentDto>(response);

        department.Members.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateDepartment_ShouldToggleActiveState()
    {
        var created = await SeedDepartmentAsync(d => d.WithName("Field Ops"));

        var update = new DepartmentUpdateRequest
        {
            Name = created.Name,
            Description = "updated",
            IsActive = false,
            Members = created.Members.Select(m => new DepartmentMemberRequest
            {
                Id = m.Id,
                Email = m.Email,
                FullName = m.FullName,
                IsActive = false,
                NotifyOnTicketEmail = m.NotifyOnTicketEmail
            }).ToArray()
        };

        using var response = await Client.PutAsync($"/departments/{created.Id}", AsJson(update));
        var department = await DeserializeAsync<DepartmentDto>(response);

        department.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_ShouldIncludeInactive_WhenRequested()
    {
        await SeedDepartmentAsync(d => d.AsInactive().WithName("A"));
        await SeedDepartmentAsync(d => d.WithName("B"));

        using var activeOnly = await Client.GetAsync("/departments?includeInactive=false");
        var activeDepartments = await DeserializeAsync<List<DepartmentDto>>(activeOnly);
        activeDepartments.Should().OnlyContain(d => d.IsActive);

        using var includeInactive = await Client.GetAsync("/departments?includeInactive=true");
        var all = await DeserializeAsync<List<DepartmentDto>>(includeInactive);
        all.Should().HaveCount(2);
    }
}

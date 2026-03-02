using System.Collections.Generic;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Ticket.Data;
using Ticket.Data.Querying;
using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using Ticket.DTOs.Requests;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Unit.Domain;

public class TicketSearchScopeTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public TicketSearchScopeTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public void TitleOnly_Should_IgnoreDescriptionMatches()
    {
        var category = new CategoryBuilder().WithId(10).WithName("Infra").BuildEntity();
        var department = new DepartmentBuilder().WithId(20).WithName("Ops").BuildEntity();
        department.Members.Clear();
        var ticketInTitle = new TicketBuilder()
            .WithCategory(category.Id, category.Name)
            .WithDepartment(department.Id, department.Name)
            .WithTitle("Printer failure")
            .WithDescription("Mail server issue")
            .BuildEntity();
        var ticketInDescription = new TicketBuilder()
            .WithCategory(category.Id, category.Name)
            .WithDepartment(department.Id, department.Name)
            .WithTitle("Mail issue")
            .WithDescription("Printer failure occurs in basement")
            .BuildEntity();

        ticketInTitle.Category = category;
        ticketInDescription.Category = category;
        ticketInTitle.Department = department;
        ticketInDescription.Department = department;

        _context.AddRange(ticketInTitle, ticketInDescription);
        _context.SaveChanges();

        var parameters = new TicketQueryParameters
        {
            SearchTerm = "printer",
            SearchScope = TicketSearchScope.TitleOnly
        };

        var results = _context.Tickets.ApplyFilters(parameters).ToList();
        results.Should().ContainSingle(t => t.Title == ticketInTitle.Title);
    }

    [Fact]
    public void FullContent_Should_MatchDescriptionsAndContacts()
    {
        var category = new CategoryBuilder().BuildEntity();
        var department = new DepartmentBuilder().BuildEntity();
        department.Members.Clear();
        var ticket = new TicketBuilder()
            .WithCategory(category.Id, category.Name)
            .WithDepartment(department.Id, department.Name)
            .WithTitle("VPN ticket")
            .WithDescription("Investigate printer spooler failure")
            .WithRequester("Printer Owner", "printer.owner@example.com")
            .BuildEntity();

        ticket.Category = category;
        ticket.Department = department;

        _context.Add(ticket);
        _context.SaveChanges();

        var parameters = new TicketQueryParameters
        {
            SearchTerm = "owner@example.com",
            SearchScope = TicketSearchScope.FullContent
        };

        var results = _context.Tickets.ApplyFilters(parameters).ToList();
        results.Should().HaveCount(1);
    }

    [Fact]
    public void DepartmentFilter_Should_Combine_WithSearchScope()
    {
        var category = new CategoryBuilder().BuildEntity();
        var deptA = new DepartmentBuilder().WithId(10).WithName("Dept A").BuildEntity();
        deptA.Members.Clear();
        var deptB = new DepartmentBuilder().WithId(11).WithName("Dept B").BuildEntity();
        deptB.Members.Clear();

        var expected = new TicketBuilder()
            .WithCategory(category.Id, category.Name)
            .WithDepartment(deptA.Id, deptA.Name)
            .WithTitle("Laptop issue")
            .BuildEntity();
        var other = new TicketBuilder()
            .WithCategory(category.Id, category.Name)
            .WithDepartment(deptB.Id, deptB.Name)
            .WithTitle("Laptop issue")
            .BuildEntity();

        expected.Category = category;
        expected.Department = deptA;
        other.Category = category;
        other.Department = deptB;

        _context.AddRange(expected, other);
        _context.SaveChanges();

        var parameters = new TicketQueryParameters
        {
            SearchTerm = "laptop",
            SearchScope = TicketSearchScope.TitleOnly,
            DepartmentIds = new List<int> { deptA.Id }
        };

        var results = _context.Tickets.ApplyFilters(parameters).ToList();
        results.Should().ContainSingle(t => t.DepartmentId == deptA.Id);
    }

    [Fact]
    public void RecipientFilter_Should_RespectOptionalScope()
    {
        var category = new CategoryBuilder().BuildEntity();
        var department = new DepartmentBuilder().BuildEntity();
        department.Members.Clear();
        var ticket = new TicketBuilder()
            .WithCategory(category.Id, category.Name)
            .WithDepartment(department.Id, department.Name)
            .WithTitle("Server issue")
            .WithRecipient("Finance", "finance.ops@example.com")
            .BuildEntity();

        ticket.Category = category;
        ticket.Department = department;

        _context.Add(ticket);
        _context.SaveChanges();

        var parameters = new TicketQueryParameters
        {
            Recipient = "Finance",
            SearchScope = TicketSearchScope.FullContent
        };

        var results = _context.Tickets.ApplyFilters(parameters).ToList();
        results.Should().ContainSingle();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

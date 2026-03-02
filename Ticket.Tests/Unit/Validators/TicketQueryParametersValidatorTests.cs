using System.Collections.Generic;
using FluentAssertions;
using Ticket.Domain.Enums;
using Ticket.DTOs.Requests;
using Ticket.Validators;

namespace Ticket.Tests.Unit.Validators;

public class TicketQueryParametersValidatorTests
{
    private readonly TicketQueryParametersValidator _validator = new();

    public static IEnumerable<object[]> InvalidQueries()
    {
        yield return Build(q => q.PageSize = 0, nameof(TicketQueryParameters.PageSize));
        yield return Build(q => q.PageSize = 101, nameof(TicketQueryParameters.PageSize));
        yield return Build(q =>
        {
            q.Page = 0;
            q.PageToken = string.Empty;
        }, nameof(TicketQueryParameters.Page));
        yield return Build(q =>
        {
            q.Page = 2;
            q.PageToken = "cursor";
        }, nameof(TicketQueryParameters.Page));
        yield return Build(q => q.PageToken = new string('a', 600), nameof(TicketQueryParameters.PageToken));
        yield return Build(q =>
        {
            q.CreatedFrom = DateTimeOffset.UtcNow;
            q.CreatedTo = DateTimeOffset.UtcNow.AddDays(-2);
        }, nameof(TicketQueryParameters.CreatedTo));
        yield return Build(q =>
        {
            q.DueFrom = DateTimeOffset.UtcNow;
            q.DueTo = DateTimeOffset.UtcNow.AddDays(-1);
        }, nameof(TicketQueryParameters.DueTo));
        yield return Build(q => q.CategoryIds = new List<int> { 1, 0 }, "CategoryIds[1]");
        yield return Build(q => q.DepartmentIds = new List<int> { -5 }, "DepartmentIds[0]");
        yield return Build(q => q.DepartmentName = new string('d', 205), nameof(TicketQueryParameters.DepartmentName));
    }

    public static IEnumerable<object[]> ValidQueries()
    {
        yield return new object[]
        {
            new TicketQueryParameters
            {
                Page = 1,
                PageSize = 25,
                SortBy = TicketSortBy.Priority,
                CreatedFrom = DateTimeOffset.UtcNow.AddDays(-10),
                CreatedTo = DateTimeOffset.UtcNow
            }
        };

        yield return new object[]
        {
            new TicketQueryParameters
            {
                Page = 1,
                PageSize = 50,
                PageToken = "opaque-token",
                SortDirection = SortDirection.Desc
            }
        };

        yield return new object[]
        {
            new TicketQueryParameters
            {
                DepartmentIds = new List<int> { 5, 6 },
                CategoryIds = new List<int> { 2 },
                Statuses = new List<TicketStatus> { TicketStatus.New, TicketStatus.InProgress },
                Priorities = new List<TicketPriority> { TicketPriority.Critical }
            }
        };
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public void Should_Fail_ForInvalidModels(TicketQueryParameters query, string expectedProperty)
    {
        var result = _validator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == expectedProperty);
    }

    [Theory]
    [MemberData(nameof(ValidQueries))]
    public void Should_Pass_ForValidModels(TicketQueryParameters query)
    {
        var result = _validator.Validate(query);
        result.IsValid.Should().BeTrue();
    }

    private static object[] Build(Action<TicketQueryParameters> mutate, string property)
    {
        var model = new TicketQueryParameters
        {
            Page = 1,
            PageSize = 25
        };
        mutate(model);
        return new object[] { model, property };
    }
}

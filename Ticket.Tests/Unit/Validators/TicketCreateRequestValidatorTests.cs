using System.Collections.Generic;
using FluentAssertions;
using Ticket.Domain.Enums;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.Validators;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Unit.Validators;

public class TicketCreateRequestValidatorTests
{
    private static readonly TicketCreateRequest ValidRequest = new TicketBuilder()
        .WithCategory(1, "Support")
        .WithDepartment(1, "Ops")
        .WithPriority(TicketPriority.High)
        .BuildCreateRequest();

    private readonly TicketCreateRequestValidator _validator = new();

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return Case(builder => builder.WithTitle(string.Empty).BuildCreateRequest(), nameof(TicketCreateRequest.Title));
        yield return Case(builder => builder.WithTitle(new string('a', 205)).BuildCreateRequest(), nameof(TicketCreateRequest.Title));
        yield return Case(builder => builder.WithDescription("").BuildCreateRequest(), nameof(TicketCreateRequest.Description));
        yield return Case(builder => builder.WithCategory(0).BuildCreateRequest(), nameof(TicketCreateRequest.CategoryId));
        yield return Case(builder => builder.WithDepartment(0).BuildCreateRequest(), nameof(TicketCreateRequest.DepartmentId));
        yield return Case(builder =>
        {
            var request = builder.BuildCreateRequest();
            request.Requester = null;
            return request;
        }, nameof(TicketCreateRequest.Requester));
        yield return Case(builder =>
        {
            var request = builder.BuildCreateRequest();
            request.Requester = new TicketContactInfoDto { Name = "User", Email = "invalid" };
            return request;
        }, "Requester.Email");
        yield return Case(builder =>
        {
            var request = builder.BuildCreateRequest();
            request.ReferenceCode = new string('x', 101);
            return request;
        }, nameof(TicketCreateRequest.ReferenceCode));
        yield return Case(builder =>
        {
            var request = builder.BuildCreateRequest();
            request.DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
            return request;
        }, nameof(TicketCreateRequest.DueAtUtc));
        yield return Case(builder =>
        {
            var request = builder.BuildCreateRequest();
            request.Requester = new TicketContactInfoDto
            {
                Name = "User",
                Email = $"{new string('a', 330)}@example.com"
            };
            return request;
        }, "Requester.Email");
    }

    public static IEnumerable<object[]> ValidRequests()
    {
        yield return new object[] { ValidRequest };
        yield return new object[]
        {
            new TicketBuilder()
                .WithTitle("Printer")
                .WithDepartment(2, "Field Ops")
                .WithCategory(5, "Facilities")
                .WithPriority(TicketPriority.Low)
                .BuildCreateRequest()
        };
        yield return new object[]
        {
            new TicketBuilder()
                .WithPriority(TicketPriority.Critical)
                .WithDueDate(DateTimeOffset.UtcNow.AddDays(2))
                .WithReferenceCode("REF-123")
                .BuildCreateRequest()
        };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void Should_Fail_ForInvalidRequests(TicketCreateRequest request, string expectedProperty)
    {
        var validation = _validator.Validate(request);
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.PropertyName == expectedProperty);
    }

    [Theory]
    [MemberData(nameof(ValidRequests))]
    public void Should_Pass_ForValidRequests(TicketCreateRequest request)
    {
        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    private static object[] Case(Func<TicketBuilder, TicketCreateRequest> factory, string property)
        => new object[] { factory(new TicketBuilder()), property };
}

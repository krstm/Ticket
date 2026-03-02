using System.Collections.Generic;
using FluentAssertions;
using Ticket.Domain.Enums;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.Validators;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Unit.Validators;

public class TicketUpdateRequestValidatorTests
{
    private readonly TicketUpdateRequestValidator _validator = new();

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return Build("ActorNull", request =>
        {
            request.Actor = null!;
        }, nameof(TicketUpdateRequest.Actor));

        yield return Build("ActorMissingName", request =>
        {
            request.Actor.Name = string.Empty;
        }, "Actor.Name");

        yield return Build("ActorInvalidEmail", request =>
        {
            request.Actor.Email = "invalid";
        }, "Actor.Email");

        yield return Build("ActorTypeOutOfRange", request =>
        {
            request.Actor.ActorType = (TicketActorType)999;
        }, "Actor.ActorType");

        yield return Build("MissingDescription", request =>
        {
            request.Description = string.Empty;
        }, nameof(TicketUpdateRequest.Description));

        yield return Build("ShortTitle", request =>
        {
            request.Title = string.Empty;
        }, nameof(TicketUpdateRequest.Title));
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void Should_Fail_ForInvalidRequest(TicketUpdateRequest request, string expectedProperty)
    {
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == expectedProperty);
    }

    [Fact]
    public void Should_Pass_ForValidUpdate()
    {
        var request = new TicketBuilder()
            .WithPriority(TicketPriority.Critical)
            .WithDueDate(DateTimeOffset.UtcNow.AddDays(1))
            .WithReferenceCode("REF-900")
            .BuildUpdateRequest();

        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    private static object[] Build(string name, Action<TicketUpdateRequest> mutate, string property)
    {
        var request = new TicketBuilder().BuildUpdateRequest();
        mutate(request);
        return new object[] { request, property };
    }
}

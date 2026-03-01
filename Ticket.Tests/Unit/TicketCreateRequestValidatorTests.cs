using Ticket.Domain.Enums;
using Ticket.DTOs.Requests;
using Ticket.Validators;

namespace Ticket.Tests.Unit;

public class TicketCreateRequestValidatorTests
{
    private readonly TicketCreateRequestValidator _validator = new();

    [Fact]
    public void Should_Fail_When_TitleMissing()
    {
        var model = new TicketCreateRequest
        {
            Description = "desc",
            CategoryId = 1
        };

        var result = _validator.Validate(model);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(TicketCreateRequest.Title));
    }

    [Fact]
    public void Should_Pass_ForValidRequest()
    {
        var model = new TicketCreateRequest
        {
            Title = "Printer",
            Description = "desc",
            CategoryId = 2,
            Priority = TicketPriority.Low
        };

        var result = _validator.Validate(model);
        Assert.True(result.IsValid);
    }
}

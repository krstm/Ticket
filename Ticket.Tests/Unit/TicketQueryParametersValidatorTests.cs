using Ticket.DTOs.Requests;
using Ticket.Validators;

namespace Ticket.Tests.Unit;

public class TicketQueryParametersValidatorTests
{
    private readonly TicketQueryParametersValidator _validator = new();

    [Fact]
    public void Should_Fail_When_PageSizeTooLarge()
    {
        var model = new TicketQueryParameters
        {
            Page = 1,
            PageSize = 1000
        };

        var result = _validator.Validate(model);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Should_Pass_ForValidQuery()
    {
        var model = new TicketQueryParameters
        {
            Page = 1,
            PageSize = 25,
            CreatedFrom = DateTimeOffset.UtcNow.AddDays(-10),
            CreatedTo = DateTimeOffset.UtcNow
        };

        var result = _validator.Validate(model);
        Assert.True(result.IsValid);
    }
}

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
            CategoryId = 1,
            DepartmentId = 1,
            Requester = new()
            {
                Name = "User",
                Email = "user@example.com"
            }
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
            DepartmentId = 3,
            Priority = TicketPriority.Low,
            Requester = new()
            {
                Name = "Valid User",
                Email = "valid@example.com"
            }
        };

        var result = _validator.Validate(model);
        Assert.True(result.IsValid);
    }
}

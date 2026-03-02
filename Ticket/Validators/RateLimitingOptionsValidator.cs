using FluentValidation;
using Ticket.Configuration;

namespace Ticket.Validators;

public class RateLimitingOptionsValidator : AbstractValidator<RateLimitingOptions>
{
    public RateLimitingOptionsValidator()
    {
        RuleFor(x => x.PermitLimit)
            .GreaterThan(0);

        RuleFor(x => x.WindowSeconds)
            .GreaterThan(0);

        RuleFor(x => x.QueueLimit)
            .GreaterThanOrEqualTo(0);
    }
}

using FluentValidation;
using Ticket.DTOs.Requests;

namespace Ticket.Validators;

public class TicketQueryParametersValidator : AbstractValidator<TicketQueryParameters>
{
    public TicketQueryParametersValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .When(x => string.IsNullOrWhiteSpace(x.PageToken));

        RuleFor(x => x.Page)
            .Equal(1)
            .When(x => !string.IsNullOrWhiteSpace(x.PageToken))
            .WithMessage("Page must be 1 when using page tokens.");

        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.PageToken)
            .MaximumLength(512)
            .When(x => !string.IsNullOrWhiteSpace(x.PageToken));

        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom)
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);

        RuleFor(x => x.DueTo)
            .GreaterThanOrEqualTo(x => x.DueFrom)
            .When(x => x.DueFrom.HasValue && x.DueTo.HasValue);

        RuleForEach(x => x.CategoryIds).GreaterThan(0);
    }
}

using FluentValidation;
using Ticket.DTOs.Requests;

namespace Ticket.Validators;

public class TicketQueryParametersValidator : AbstractValidator<TicketQueryParameters>
{
    public TicketQueryParametersValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom)
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);

        RuleFor(x => x.DueTo)
            .GreaterThanOrEqualTo(x => x.DueFrom)
            .When(x => x.DueFrom.HasValue && x.DueTo.HasValue);

        RuleForEach(x => x.CategoryIds).GreaterThan(0);
    }
}

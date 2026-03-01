using FluentValidation;
using Ticket.DTOs.Requests;

namespace Ticket.Validators;

public class ReportQueryValidator : AbstractValidator<ReportQuery>
{
    public ReportQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}

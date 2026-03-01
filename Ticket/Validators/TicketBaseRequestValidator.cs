using FluentValidation;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;

namespace Ticket.Validators;

public abstract class TicketBaseRequestValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : TicketCreateRequest
{
    protected TicketBaseRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(5000);

        RuleFor(x => x.CategoryId)
            .GreaterThan(0);

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0);

        RuleFor(x => x.ReferenceCode)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.ReferenceCode));

        RuleFor(x => x.DueAtUtc)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(x => x.DueAtUtc.HasValue);

        RuleFor(x => x.Requester)
            .NotNull().WithMessage("Requester information is required.");

        When(x => x.Requester != null, () =>
        {
            RuleFor(x => x.Requester!)
                .SetValidator(new TicketContactInfoDtoValidator()!);

            RuleFor(x => x.Requester!.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(320);
        });

        RuleFor(x => x.Recipient)
            .SetValidator(new TicketContactInfoDtoValidator()!)
            .When(x => x.Recipient != null);
    }
}

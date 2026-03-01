using FluentValidation;
using Ticket.DTOs.Requests;

namespace Ticket.Validators;

public class TicketCommentCreateRequestValidator : AbstractValidator<TicketCommentCreateRequest>
{
    public TicketCommentCreateRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.Actor)
            .NotNull()
            .SetValidator(new TicketActorContextValidator());
    }
}

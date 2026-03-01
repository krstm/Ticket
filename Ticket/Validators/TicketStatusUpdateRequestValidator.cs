using FluentValidation;
using Ticket.DTOs.Requests;

namespace Ticket.Validators;

public class TicketStatusUpdateRequestValidator : AbstractValidator<TicketStatusUpdateRequest>
{
    public TicketStatusUpdateRequestValidator()
    {
        RuleFor(x => x.ChangedBy)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Note)
            .MaximumLength(2000);
    }
}

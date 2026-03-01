using FluentValidation;
using Ticket.DTOs.Common;

namespace Ticket.Validators;

public class TicketActorContextValidator : AbstractValidator<TicketActorContextDto>
{
    public TicketActorContextValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ActorType)
            .IsInEnum();
    }
}

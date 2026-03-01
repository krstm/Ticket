using FluentValidation;
using Ticket.DTOs.Requests;

namespace Ticket.Validators;

public class TicketUpdateRequestValidator : TicketBaseRequestValidator<TicketUpdateRequest>
{
    public TicketUpdateRequestValidator()
    {
        RuleFor(x => x.Actor)
            .NotNull()
            .SetValidator(new TicketActorContextValidator());
    }
}

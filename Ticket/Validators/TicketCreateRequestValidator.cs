using FluentValidation;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;

namespace Ticket.Validators;

public class TicketCreateRequestValidator : TicketBaseRequestValidator<TicketCreateRequest>
{
}

public class TicketContactInfoDtoValidator : AbstractValidator<TicketContactInfoDto>
{
    public TicketContactInfoDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}

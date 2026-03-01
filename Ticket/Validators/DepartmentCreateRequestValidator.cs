using FluentValidation;
using Ticket.DTOs.Requests;

namespace Ticket.Validators;

public class DepartmentCreateRequestValidator : AbstractValidator<DepartmentCreateRequest>
{
    public DepartmentCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleForEach(x => x.Members)
            .SetValidator(new DepartmentMemberRequestValidator());
    }
}

public class DepartmentUpdateRequestValidator : AbstractValidator<DepartmentUpdateRequest>
{
    public DepartmentUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleForEach(x => x.Members)
            .SetValidator(new DepartmentMemberRequestValidator());
    }
}

public class DepartmentMemberRequestValidator : AbstractValidator<DepartmentMemberRequest>
{
    public DepartmentMemberRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
    }
}

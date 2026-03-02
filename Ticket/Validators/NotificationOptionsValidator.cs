using FluentValidation;
using Ticket.Configuration;

namespace Ticket.Validators;

public class NotificationOptionsValidator : AbstractValidator<NotificationOptions>
{
    private static readonly HashSet<string> AllowedChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "log",
        "email",
        "webhook"
    };

    public NotificationOptionsValidator()
    {
        RuleFor(x => x.PreferredChannel)
            .NotEmpty()
            .Must(channel => AllowedChannels.Contains(channel))
            .WithMessage("PreferredChannel must be one of: none, log, email, webhook.");

        RuleFor(x => x)
            .Must(o => !o.NotifyOnTicketResolved || o.NotifyOnTicketCreated)
            .WithMessage("NotifyOnTicketResolved can only be enabled when creation notifications are enabled.");
    }
}

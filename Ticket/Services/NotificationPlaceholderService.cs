using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticket.Configuration;
using Ticket.Interfaces.Services;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Services;

public class NotificationPlaceholderService : INotificationService
{
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationPlaceholderService> _logger;

    public NotificationPlaceholderService(IOptions<NotificationOptions> options, ILogger<NotificationPlaceholderService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task NotifyTicketCreatedAsync(TicketEntity ticket, string createdBy, CancellationToken ct)
    {
        if (!_options.NotifyOnTicketCreated)
        {
            _logger.LogDebug("Ticket {TicketId} creation notification suppressed (disabled).", ticket.Id);
            return Task.CompletedTask;
        }

        _logger.LogInformation("[Notification Stub] Ticket {TicketId} created by {User} would trigger {Channel} channel.",
            ticket.Id,
            createdBy,
            _options.PreferredChannel);
        return Task.CompletedTask;
    }

    public Task NotifyTicketResolvedAsync(TicketEntity ticket, string changedBy, string? note, CancellationToken ct)
    {
        if (!_options.NotifyOnTicketResolved)
        {
            _logger.LogDebug("Ticket {TicketId} resolve notification suppressed (disabled).", ticket.Id);
            return Task.CompletedTask;
        }

        _logger.LogInformation("[Notification Stub] Ticket {TicketId} resolved by {User}. Note: {Note}. Channel: {Channel}.",
            ticket.Id,
            changedBy,
            note,
            _options.PreferredChannel);
        return Task.CompletedTask;
    }
}

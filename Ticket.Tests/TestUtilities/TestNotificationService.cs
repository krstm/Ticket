using Ticket.Interfaces.Services;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Tests.TestUtilities;

public class TestNotificationService : INotificationService
{
    private readonly List<(Guid TicketId, string CreatedBy)> _createdEvents = new();
    private readonly List<(Guid TicketId, string ChangedBy, string? Note)> _resolvedEvents = new();

    public IReadOnlyCollection<(Guid TicketId, string CreatedBy)> CreatedEvents => _createdEvents;
    public IReadOnlyCollection<(Guid TicketId, string ChangedBy, string? Note)> ResolvedEvents => _resolvedEvents;

    public Task NotifyTicketCreatedAsync(TicketEntity ticket, string createdBy, CancellationToken ct)
    {
        _createdEvents.Add((ticket.Id, createdBy));
        return Task.CompletedTask;
    }

    public Task NotifyTicketResolvedAsync(TicketEntity ticket, string changedBy, string? note, CancellationToken ct)
    {
        _resolvedEvents.Add((ticket.Id, changedBy, note));
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _createdEvents.Clear();
        _resolvedEvents.Clear();
    }
}

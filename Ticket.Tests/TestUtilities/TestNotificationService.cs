using Ticket.Interfaces.Services;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Tests.TestUtilities;

public class TestNotificationService : INotificationService
{
    private readonly List<(Guid TicketId, string CreatedBy, IReadOnlyCollection<string> Recipients)> _createdEvents = new();
    private readonly List<(Guid TicketId, string ChangedBy, string? Note, IReadOnlyCollection<string> Recipients)> _resolvedEvents = new();
    private readonly List<(Guid TicketId, long CommentId, string AuthorEmail, IReadOnlyCollection<string> Recipients)> _commentEvents = new();

    public IReadOnlyCollection<(Guid TicketId, string CreatedBy, IReadOnlyCollection<string> Recipients)> CreatedEvents => _createdEvents;
    public IReadOnlyCollection<(Guid TicketId, string ChangedBy, string? Note, IReadOnlyCollection<string> Recipients)> ResolvedEvents => _resolvedEvents;
    public IReadOnlyCollection<(Guid TicketId, long CommentId, string AuthorEmail, IReadOnlyCollection<string> Recipients)> CommentEvents => _commentEvents;

    public Task NotifyTicketCreatedAsync(TicketEntity ticket, string createdBy, IReadOnlyCollection<string> recipientEmails, CancellationToken ct)
    {
        _createdEvents.Add((ticket.Id, createdBy, recipientEmails));
        return Task.CompletedTask;
    }

    public Task NotifyTicketResolvedAsync(TicketEntity ticket, string changedBy, string? note, IReadOnlyCollection<string> recipientEmails, CancellationToken ct)
    {
        _resolvedEvents.Add((ticket.Id, changedBy, note, recipientEmails));
        return Task.CompletedTask;
    }

    public Task NotifyTicketCommentAddedAsync(TicketEntity ticket, Ticket.Domain.Entities.TicketComment comment, IReadOnlyCollection<string> recipientEmails, CancellationToken ct)
    {
        _commentEvents.Add((ticket.Id, comment.Id, comment.AuthorEmail, recipientEmails));
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _createdEvents.Clear();
        _resolvedEvents.Clear();
        _commentEvents.Clear();
    }
}

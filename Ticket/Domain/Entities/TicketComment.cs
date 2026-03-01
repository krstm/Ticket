using Ticket.Domain.Enums;

namespace Ticket.Domain.Entities;

public class TicketComment
{
    public long Id { get; set; }
    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public string Body { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public string AuthorEmailNormalized { get; set; } = string.Empty;
    public TicketCommentSource Source { get; set; } = TicketCommentSource.Requester;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

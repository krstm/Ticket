using Ticket.Domain.Enums;

namespace Ticket.DTOs.Responses;

public class TicketCommentDto
{
    public long Id { get; set; }
    public Guid TicketId { get; set; }
    public string Body { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public TicketCommentSource Source { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

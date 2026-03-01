using Ticket.Domain.Enums;

namespace Ticket.Domain.Entities;

public class TicketHistory
{
    public long Id { get; set; }
    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public TicketStatus Status { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string ChangedBy { get; set; } = "system";
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

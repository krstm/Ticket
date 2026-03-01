using Ticket.Domain.Enums;

namespace Ticket.DTOs.Responses;

public class TicketHistoryDto
{
    public long Id { get; set; }
    public TicketStatus Status { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
}

using Ticket.Domain.Enums;

namespace Ticket.DTOs.Responses;

public class TicketSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public string? ReferenceCode { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

using Ticket.Domain.Enums;

namespace Ticket.DTOs.ViewModels;

public class TimelineItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
}

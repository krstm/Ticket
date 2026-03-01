using Ticket.Domain.Enums;
using Ticket.Domain.ValueObjects;

namespace Ticket.Domain.Entities;

public class Ticket : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketStatus Status { get; set; } = TicketStatus.New;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public TicketContactInfo Requester { get; set; } = TicketContactInfo.Empty;
    public TicketContactInfo Recipient { get; set; } = TicketContactInfo.Empty;
    public TicketMetadata Metadata { get; set; } = TicketMetadata.Empty;

    public DateTimeOffset? DueAtUtc { get; set; }
    public string? ReferenceCode { get; set; }

    public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();
}

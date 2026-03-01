using Ticket.Domain.Enums;
using Ticket.DTOs.Common;

namespace Ticket.DTOs.Requests;

public class TicketCreateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public int CategoryId { get; set; }
    public int DepartmentId { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public string? ReferenceCode { get; set; }
    public TicketContactInfoDto? Requester { get; set; }
    public TicketContactInfoDto? Recipient { get; set; }
    public TicketMetadataDto? Metadata { get; set; }
}

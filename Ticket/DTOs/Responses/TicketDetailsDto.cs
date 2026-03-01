using Ticket.DTOs.Common;

namespace Ticket.DTOs.Responses;

public class TicketDetailsDto : TicketSummaryDto
{
    public string Description { get; set; } = string.Empty;
    public TicketContactInfoDto Requester { get; set; } = new();
    public TicketContactInfoDto Recipient { get; set; } = new();
    public TicketMetadataDto Metadata { get; set; } = new();
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public IReadOnlyCollection<TicketHistoryDto> History { get; set; } = Array.Empty<TicketHistoryDto>();
}

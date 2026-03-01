using Ticket.DTOs.Common;

namespace Ticket.DTOs.Requests;

public class TicketUpdateRequest : TicketCreateRequest
{
    public TicketActorContextDto Actor { get; set; } = new();
}

using Ticket.DTOs.Common;

namespace Ticket.DTOs.Requests;

public class TicketCommentCreateRequest
{
    public string Body { get; set; } = string.Empty;
    public TicketActorContextDto Actor { get; set; } = new();
}

using Ticket.DTOs.Common;
using Ticket.Domain.Enums;

namespace Ticket.DTOs.Requests;

public class TicketStatusUpdateRequest
{
    public TicketStatus Status { get; set; }
    public string? Note { get; set; }
    public string ChangedBy { get; set; } = "system";
    public TicketActorContextDto Actor { get; set; } = new();
}

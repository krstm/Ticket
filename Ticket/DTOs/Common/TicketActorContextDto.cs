using Ticket.Domain.Enums;

namespace Ticket.DTOs.Common;

public class TicketActorContextDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TicketActorType ActorType { get; set; } = TicketActorType.Requester;
}

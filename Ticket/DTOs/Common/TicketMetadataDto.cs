namespace Ticket.DTOs.Common;

public class TicketMetadataDto
{
    public bool IsExternal { get; set; }
    public bool RequiresFollowUp { get; set; }
    public string Channel { get; set; } = "Web";
}

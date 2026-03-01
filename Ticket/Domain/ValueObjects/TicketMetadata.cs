namespace Ticket.Domain.ValueObjects;

public record class TicketMetadata(bool IsExternal, bool RequiresFollowUp, string Channel = "Web")
{
    public static TicketMetadata Empty => new(false, false, "Web");
}

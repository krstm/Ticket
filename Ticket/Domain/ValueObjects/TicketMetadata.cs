namespace Ticket.Domain.ValueObjects;

public record class TicketMetadata(bool IsExternal, bool RequiresFollowUp)
{
    public static TicketMetadata Empty => new(false, false);
}

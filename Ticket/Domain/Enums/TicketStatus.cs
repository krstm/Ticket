namespace Ticket.Domain.Enums;

public enum TicketStatus
{
    New = 0,
    InProgress = 1,
    AwaitingResponse = 2,
    Resolved = 3,
    Closed = 4,
    Rejected = 5
}

using Ticket.Domain.Enums;

namespace Ticket.Domain.ValueObjects;

public sealed record TicketActorContext(
    string DisplayName,
    string Email,
    TicketActorType ActorType)
{
    public string NormalizedEmail => Email.Trim().ToUpperInvariant();
}

namespace Ticket.Domain.ValueObjects;

public record class TicketContactInfo(string? Name, string? Email, string? Phone)
{
    public static TicketContactInfo Empty => new(null, null, null);

    public bool HasValue =>
        !string.IsNullOrWhiteSpace(Name) ||
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(Phone);
}

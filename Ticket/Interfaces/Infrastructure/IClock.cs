namespace Ticket.Interfaces.Infrastructure;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

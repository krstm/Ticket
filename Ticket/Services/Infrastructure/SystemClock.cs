using Ticket.Interfaces.Infrastructure;

namespace Ticket.Services.Infrastructure;

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

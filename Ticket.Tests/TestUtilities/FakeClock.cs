using Ticket.Interfaces.Infrastructure;

namespace Ticket.Tests.TestUtilities;

public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; }

    public FakeClock(DateTimeOffset? seed = null)
    {
        UtcNow = seed ?? DateTimeOffset.Parse("2024-01-01T00:00:00Z");
    }

    public void Advance(TimeSpan delta) => UtcNow += delta;

    public void Set(DateTimeOffset value) => UtcNow = value;
}

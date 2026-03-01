using MediatR;

namespace Ticket.Domain.Events;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredOnUtc { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOnUtc { get; }

    protected DomainEventBase(DateTimeOffset occurredOnUtc)
    {
        OccurredOnUtc = occurredOnUtc;
    }
}

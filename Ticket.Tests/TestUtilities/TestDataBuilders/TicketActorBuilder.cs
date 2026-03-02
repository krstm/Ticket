using Ticket.Domain.Enums;
using Ticket.Domain.ValueObjects;
using Ticket.DTOs.Common;

namespace Ticket.Tests.TestUtilities.TestDataBuilders;

public class TicketActorBuilder
{
    private string _name = "Requester";
    private string _email = "requester@example.com";
    private TicketActorType _actorType = TicketActorType.Requester;

    public TicketActorBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public TicketActorBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public TicketActorBuilder AsDepartmentMember()
    {
        _actorType = TicketActorType.DepartmentMember;
        return this;
    }

    public TicketActorBuilder AsRequester()
    {
        _actorType = TicketActorType.Requester;
        return this;
    }

    public TicketActorContextDto BuildDto() => new()
    {
        Name = _name,
        Email = _email,
        ActorType = _actorType
    };

    public TicketActorContext BuildValueObject() => new(_name, _email, _actorType);
}

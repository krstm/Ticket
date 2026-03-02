using FluentAssertions;
using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using Ticket.Domain.Support;
using Ticket.Domain.ValueObjects;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Unit.Services;

public class TicketDomainEventFactoryTests
{
    private readonly Department _department;
    private readonly Ticket.Domain.Entities.Ticket _ticket;
    private readonly TicketActorContext _actor;

    public TicketDomainEventFactoryTests()
    {
        _department = new DepartmentBuilder()
            .WithoutMembers()
            .WithMember("Active", "active@example.com")
            .WithMember("Inactive", "inactive@example.com", isActive: false)
            .BuildEntity();

        _ticket = new TicketBuilder()
            .WithDepartment(_department.Id, _department.Name)
            .WithCategory(1, "Support")
            .BuildEntity();

        _actor = new TicketActorBuilder()
            .WithName("Ops Agent")
            .WithEmail("ops@example.com")
            .AsDepartmentMember()
            .BuildValueObject();
    }

    [Fact]
    public void CreatedEvent_Should_IncludeActiveMembersOnly()
    {
        var evt = TicketDomainEventFactory.Created(_ticket, "creator", "REF-1", DateTimeOffset.UtcNow, _department);
        evt.DepartmentMembers.Should().OnlyContain(m => m.IsActive);
    }

    [Fact]
    public void StatusChangedEvent_Should_MapActorFields()
    {
        var evt = TicketDomainEventFactory.StatusChanged(
            _ticket,
            TicketStatus.New,
            TicketStatus.InProgress,
            _actor,
            "Investigating",
            DateTimeOffset.UtcNow,
            _department);

        evt.ChangedBy.Should().Be(_actor.DisplayName);
        evt.ChangedByEmail.Should().Be(_actor.Email);
        evt.DepartmentMembers.Should().HaveCount(1);
    }

    [Fact]
    public void ResolvedEvent_Should_CaptureNoteAndEmail()
    {
        var evt = TicketDomainEventFactory.Resolved(
            _ticket,
            _actor,
            "Done",
            DateTimeOffset.UtcNow,
            _department);

        evt.Note.Should().Be("Done");
        evt.ChangedByEmail.Should().Be(_actor.Email);
    }

    [Fact]
    public void CommentAddedEvent_Should_MapCommentDetails()
    {
        var comment = new TicketComment
        {
            Id = 10,
            TicketId = _ticket.Id,
            Body = "Update",
            AuthorDisplayName = "Ops Agent",
            AuthorEmail = "ops@example.com",
            AuthorEmailNormalized = "OPS@EXAMPLE.COM",
            Source = TicketCommentSource.DepartmentMember,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var evt = TicketDomainEventFactory.CommentAdded(_ticket, comment, _department);
        evt.Comment.Should().Be(comment);
        evt.AuthorEmail.Should().Be(comment.AuthorEmail);
        evt.DepartmentMembers.Should().HaveCount(1);
    }
}

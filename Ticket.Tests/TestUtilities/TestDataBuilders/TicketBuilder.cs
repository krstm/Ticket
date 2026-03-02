using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using Ticket.Domain.ValueObjects;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Tests.TestUtilities.TestDataBuilders;

public class TicketBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _title = "Email outage";
    private string _description = "Cannot send emails";
    private TicketPriority _priority = TicketPriority.High;
    private TicketStatus _status = TicketStatus.New;
    private int _categoryId = 1;
    private string _categoryName = "Support";
    private int _departmentId = 1;
    private string _departmentName = "IT Operations";
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow.AddHours(-1);
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _dueAt;
    private string? _referenceCode = "REF-1";
    private TicketActorContextDto _requesterActor = new TicketActorBuilder().WithName("Requester User").WithEmail("requester@example.com").AsRequester().BuildDto();
    private TicketActorContextDto _departmentActor = new TicketActorBuilder().WithName("Ops Agent").WithEmail("ops.agent@example.com").AsDepartmentMember().BuildDto();
    private TicketContactInfoDto _requester = new() { Name = "Requester User", Email = "requester@example.com" };
    private TicketContactInfoDto _recipient = new() { Name = "Recipient User", Email = "recipient@example.com" };
    private TicketMetadataDto _metadata = new() { Channel = "Web", IsExternal = false, RequiresFollowUp = true };
    private TicketContactInfoDto _customActorContact = new() { Name = "Requester User", Email = "requester@example.com" };
    private byte[] _rowVersion = Guid.NewGuid().ToByteArray();

    public TicketBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TicketBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TicketBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public TicketBuilder WithPriority(TicketPriority priority)
    {
        _priority = priority;
        return this;
    }

    public TicketBuilder WithStatus(TicketStatus status)
    {
        _status = status;
        return this;
    }

    public TicketBuilder WithCategory(int id, string? name = null)
    {
        _categoryId = id;
        _categoryName = name ?? _categoryName;
        return this;
    }

    public TicketBuilder WithDepartment(int id, string? name = null)
    {
        _departmentId = id;
        _departmentName = name ?? _departmentName;
        return this;
    }

    public TicketBuilder WithDueDate(DateTimeOffset? dueAtUtc)
    {
        _dueAt = dueAtUtc;
        return this;
    }

    public TicketBuilder WithReferenceCode(string? referenceCode)
    {
        _referenceCode = referenceCode;
        return this;
    }

    public TicketBuilder WithRequester(string name, string email)
    {
        _requester = new TicketContactInfoDto { Name = name, Email = email };
        _requesterActor = new TicketActorBuilder().WithName(name).WithEmail(email).AsRequester().BuildDto();
        _customActorContact = _requester;
        return this;
    }

    public TicketBuilder WithRecipient(string name, string email)
    {
        _recipient = new TicketContactInfoDto { Name = name, Email = email };
        return this;
    }

    public TicketBuilder WithMetadata(bool isExternal, bool requiresFollowUp, string channel = "Web")
    {
        _metadata = new TicketMetadataDto
        {
            IsExternal = isExternal,
            RequiresFollowUp = requiresFollowUp,
            Channel = channel
        };
        return this;
    }

    public TicketBuilder WithRowVersion(byte[] rowVersion)
    {
        _rowVersion = rowVersion;
        return this;
    }

    public TicketBuilder WithActor(TicketActorContextDto actor)
    {
        _requesterActor = actor;
        return this;
    }

    public TicketBuilder WithDepartmentActor(TicketActorContextDto actor)
    {
        _departmentActor = actor;
        return this;
    }

    public TicketBuilder WithCreatedAt(DateTimeOffset createdAt, DateTimeOffset? updatedAt = null)
    {
        _createdAt = createdAt;
        _updatedAt = updatedAt ?? createdAt;
        return this;
    }

    public TicketCreateRequest BuildCreateRequest() => new()
    {
        Title = _title,
        Description = _description,
        Priority = _priority,
        CategoryId = _categoryId,
        DepartmentId = _departmentId,
        DueAtUtc = _dueAt,
        ReferenceCode = _referenceCode,
        Requester = _requester,
        Recipient = _recipient,
        Metadata = _metadata
    };

    public TicketUpdateRequest BuildUpdateRequest() => new()
    {
        Title = _title,
        Description = _description,
        Priority = _priority,
        CategoryId = _categoryId,
        DepartmentId = _departmentId,
        DueAtUtc = _dueAt,
        ReferenceCode = _referenceCode,
        Requester = _requester,
        Recipient = _recipient,
        Metadata = _metadata,
        Actor = _requesterActor
    };

    public TicketStatusUpdateRequest BuildStatusRequest(TicketStatus status, TicketActorContextDto? actor = null, string? note = null) => new()
    {
        Status = status,
        Note = note,
        ChangedBy = actor?.Name ?? _departmentActor.Name!,
        Actor = actor ?? _departmentActor
    };

    public TicketCommentCreateRequest BuildCommentRequest(string body) => new()
    {
        Body = body,
        Actor = _departmentActor
    };

    public TicketEntity BuildEntity()
    {
        var ticket = new TicketEntity
        {
            Id = _id,
            Title = _title,
            Description = _description,
            CategoryId = _categoryId,
            Priority = _priority,
            Status = _status,
            DepartmentId = _departmentId,
            Requester = new TicketContactInfo(_requester.Name, _requester.Email, _requester.Phone),
            Recipient = new TicketContactInfo(_recipient.Name, _recipient.Email, _recipient.Phone),
            Metadata = new TicketMetadata(_metadata.IsExternal, _metadata.RequiresFollowUp, _metadata.Channel),
            CreatedAtUtc = _createdAt,
            UpdatedAtUtc = _updatedAt,
            DueAtUtc = _dueAt,
            ReferenceCode = _referenceCode,
            RowVersion = _rowVersion,
            Category = new Category { Id = _categoryId, Name = _categoryName },
            Department = new Department { Id = _departmentId, Name = _departmentName }
        };

        ticket.TitleNormalized = ticket.Title.ToUpperInvariant();
        ticket.DescriptionNormalized = ticket.Description.ToUpperInvariant();
        ticket.DepartmentNameNormalized = ticket.Department?.Name.ToUpperInvariant() ?? string.Empty;
        ticket.RequesterNameNormalized = ticket.Requester.Name?.ToUpperInvariant();
        ticket.RequesterEmailNormalized = ticket.Requester.Email?.ToUpperInvariant();
        ticket.RecipientNameNormalized = ticket.Recipient.Name?.ToUpperInvariant();
        ticket.RecipientEmailNormalized = ticket.Recipient.Email?.ToUpperInvariant();
        ticket.ReferenceCodeNormalized = ticket.ReferenceCode?.ToUpperInvariant();

        return ticket;
    }
}

using Ticket.Domain.Entities;
using Ticket.DTOs.Requests;

namespace Ticket.Tests.TestUtilities.TestDataBuilders;

public class DepartmentBuilder
{
    private int _id = 1;
    private string _name = "IT Operations";
    private string? _description = "Handles infra";
    private bool _isActive = true;
    private readonly List<DepartmentMember> _members = new();

    public DepartmentBuilder()
    {
        _members.Add(new DepartmentMember
        {
            Id = 1,
            FullName = "Ops Agent",
            Email = "ops.agent@example.com",
            EmailNormalized = "OPS.AGENT@EXAMPLE.COM",
            IsActive = true,
            NotifyOnTicketEmail = true
        });
    }

    public DepartmentBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public DepartmentBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public DepartmentBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public DepartmentBuilder WithMember(string fullName, string email, bool isActive = true, bool notify = true)
    {
        var nextId = _members.Count == 0 ? 1 : _members.Max(m => m.Id) + 1;
        _members.Add(new DepartmentMember
        {
            Id = nextId,
            FullName = fullName,
            Email = email,
            EmailNormalized = email.ToUpperInvariant(),
            IsActive = isActive,
            NotifyOnTicketEmail = notify
        });
        return this;
    }

    public DepartmentBuilder WithoutMembers()
    {
        _members.Clear();
        return this;
    }

    public DepartmentBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public Department BuildEntity() => new()
    {
        Id = _id,
        Name = _name,
        Description = _description,
        IsActive = _isActive,
        Members = _members.ToList(),
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    public DepartmentCreateRequest BuildCreateRequest() => new()
    {
        Name = _name,
        Description = _description,
        Members = _members.Select(ToRequest).ToArray()
    };

    public DepartmentUpdateRequest BuildUpdateRequest() => new()
    {
        Name = _name,
        Description = _description,
        IsActive = _isActive,
        Members = _members.Select(ToRequest).ToArray()
    };

    private static DepartmentMemberRequest ToRequest(DepartmentMember member) => new()
    {
        Id = member.Id,
        FullName = member.FullName,
        Email = member.Email,
        IsActive = member.IsActive,
        NotifyOnTicketEmail = member.NotifyOnTicketEmail
    };
}

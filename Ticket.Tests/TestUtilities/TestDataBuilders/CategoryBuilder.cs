using Ticket.Domain.Entities;
using Ticket.DTOs.Requests;

namespace Ticket.Tests.TestUtilities.TestDataBuilders;

public class CategoryBuilder
{
    private int _id = 1;
    private string _name = "General";
    private string? _description = "Auto-generated";
    private bool _isActive = true;

    public CategoryBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public CategoryBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CategoryBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public CategoryBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public Category BuildEntity() => new()
    {
        Id = _id,
        Name = _name,
        Description = _description,
        IsActive = _isActive,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    public CategoryCreateRequest BuildCreateRequest() => new()
    {
        Name = _name,
        Description = _description
    };

    public CategoryUpdateRequest BuildUpdateRequest() => new()
    {
        Name = _name,
        Description = _description,
        IsActive = _isActive
    };
}

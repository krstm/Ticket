using Ticket.Domain.Support;

namespace Ticket.Domain.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DepartmentMember> Members { get; set; } = new List<DepartmentMember>();

    public string NormalizedName => SearchNormalizer.NormalizeRequired(Name);
}

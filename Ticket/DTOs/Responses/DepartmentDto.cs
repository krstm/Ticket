namespace Ticket.DTOs.Responses;

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public IReadOnlyCollection<DepartmentMemberDto> Members { get; set; } = Array.Empty<DepartmentMemberDto>();
}

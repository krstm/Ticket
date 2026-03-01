namespace Ticket.DTOs.Requests;

public class DepartmentUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public IReadOnlyCollection<DepartmentMemberRequest> Members { get; set; } = Array.Empty<DepartmentMemberRequest>();
}

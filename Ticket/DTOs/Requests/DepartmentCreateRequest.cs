using Ticket.DTOs.Common;

namespace Ticket.DTOs.Requests;

public class DepartmentCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyCollection<DepartmentMemberRequest> Members { get; set; } = Array.Empty<DepartmentMemberRequest>();
}

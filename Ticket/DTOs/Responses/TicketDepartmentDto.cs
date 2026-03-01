namespace Ticket.DTOs.Responses;

public class TicketDepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyCollection<DepartmentMemberDto> Members { get; set; } = Array.Empty<DepartmentMemberDto>();
}

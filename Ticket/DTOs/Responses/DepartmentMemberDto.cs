namespace Ticket.DTOs.Responses;

public class DepartmentMemberDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool NotifyOnTicketEmail { get; set; }
}

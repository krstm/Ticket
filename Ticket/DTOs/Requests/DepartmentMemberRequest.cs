namespace Ticket.DTOs.Requests;

public class DepartmentMemberRequest
{
    public int? Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool NotifyOnTicketEmail { get; set; } = true;
}

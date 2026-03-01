namespace Ticket.Domain.Entities;

public class DepartmentMember
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmailNormalized { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool NotifyOnTicketEmail { get; set; } = true;
}

namespace Ticket.DTOs.Requests;

public class CategoryUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

namespace Ticket.DTOs.Requests;

public class CategoryCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

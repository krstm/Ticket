using Ticket.Domain.Enums;

namespace Ticket.DTOs.Requests;

public class TicketQueryParameters
{
    public string? SearchTerm { get; set; }
    public TicketSearchScope SearchScope { get; set; } = TicketSearchScope.FullContent;
    public IReadOnlyCollection<int>? CategoryIds { get; set; }
    public IReadOnlyCollection<TicketStatus>? Statuses { get; set; }
    public IReadOnlyCollection<TicketPriority>? Priorities { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public DateTimeOffset? DueFrom { get; set; }
    public DateTimeOffset? DueTo { get; set; }
    public string? Requester { get; set; }
    public string? Recipient { get; set; }
    public TicketSortBy SortBy { get; set; } = TicketSortBy.CreatedAt;
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? PageToken { get; set; }
}

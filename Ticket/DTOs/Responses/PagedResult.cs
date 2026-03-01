namespace Ticket.DTOs.Responses;

public class PagedResult<T>
{
    public required IReadOnlyCollection<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long? TotalCount { get; init; }
    public string? NextPageToken { get; init; }
    public int? TotalPages => TotalCount.HasValue ? (int)Math.Ceiling(TotalCount.Value / (double)PageSize) : null;
}

using Ticket.Domain.Enums;

namespace Ticket.DTOs.Requests;

public class ReportQuery
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public ReportGroupBy GroupBy { get; set; } = ReportGroupBy.Category;
    public ReportInterval Interval { get; set; } = ReportInterval.Day;
    public IReadOnlyCollection<int>? DepartmentIds { get; set; }
}

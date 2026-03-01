using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;

namespace Ticket.Interfaces.Services;

public interface IReportingService
{
    Task<IReadOnlyCollection<ReportBucketDto>> GetSummaryAsync(ReportQuery query, CancellationToken ct);
    Task<IReadOnlyCollection<ReportBucketDto>> GetStatusTrendAsync(ReportQuery query, CancellationToken ct);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticket.Configuration;
using Ticket.Data;
using Ticket.Domain.Enums;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Interfaces.Infrastructure;
using Ticket.Interfaces.Services;

namespace Ticket.Services;

public class ReportingService : IReportingService
{
    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;
    private readonly ILogger<ReportingService> _logger;

    public ReportingService(ApplicationDbContext context, IClock clock, ILogger<ReportingService> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<ReportBucketDto>> GetSummaryAsync(ReportQuery query, CancellationToken ct)
    {
        var (from, to) = NormalizeRange(query);
        var baseQuery = _context.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Department)
            .Where(t => t.CreatedAtUtc >= from && t.CreatedAtUtc <= to);

        if (query.DepartmentIds?.Count > 0)
        {
            baseQuery = baseQuery.Where(t => query.DepartmentIds.Contains(t.DepartmentId));
        }

        IQueryable<IGrouping<string, Domain.Entities.Ticket>> grouping = query.GroupBy switch
        {
            ReportGroupBy.Status => baseQuery.GroupBy(t => t.Status.ToString()),
            ReportGroupBy.Priority => baseQuery.GroupBy(t => t.Priority.ToString()),
            ReportGroupBy.Department => baseQuery.GroupBy(t => t.Department != null ? t.Department.Name : "Unknown Department"),
            _ => baseQuery.GroupBy(t => t.Category != null ? t.Category.Name : "Unknown")
        };

        var buckets = await grouping
            .Select(g => new ReportBucketDto
            {
                Bucket = g.Key,
                Count = g.Count(),
                PeriodStartUtc = from,
                PeriodEndUtc = to
            })
            .OrderByDescending(b => b.Count)
            .ToListAsync(ct);

        return buckets;
    }

    public async Task<IReadOnlyCollection<ReportBucketDto>> GetStatusTrendAsync(ReportQuery query, CancellationToken ct)
    {
        var (from, to) = NormalizeRange(query);
        var intervalSpan = query.Interval == ReportInterval.Day ? TimeSpan.FromDays(1) : TimeSpan.FromDays(7);

        var scopedTickets = await _context.Tickets.AsNoTracking()
            .Where(t => t.CreatedAtUtc >= from && t.CreatedAtUtc <= to)
            .Where(t => query.DepartmentIds == null || query.DepartmentIds.Count == 0 || query.DepartmentIds.Contains(t.DepartmentId))
            .Select(t => new { t.CreatedAtUtc, t.Status })
            .ToListAsync(ct);

        var buckets = scopedTickets
            .Select(t => new
            {
                PeriodStart = AlignToInterval(t.CreatedAtUtc, query.Interval),
                t.Status
            })
            .GroupBy(x => new { x.PeriodStart, x.Status })
            .Select(g => new ReportBucketDto
            {
                Bucket = g.Key.Status.ToString(),
                Count = g.Count(),
                PeriodStartUtc = g.Key.PeriodStart,
                PeriodEndUtc = g.Key.PeriodStart.Add(intervalSpan)
            })
            .OrderBy(b => b.PeriodStartUtc)
            .ThenBy(b => b.Bucket)
            .ToList();

        return buckets;
    }

    private (DateTimeOffset From, DateTimeOffset To) NormalizeRange(ReportQuery query)
    {
        var to = query.To ?? _clock.UtcNow;
        var from = query.From ?? to.AddDays(-30);

        if (from > to)
        {
            (from, to) = (to, from);
        }

        return (from, to);
    }

    private static DateTimeOffset AlignToInterval(DateTimeOffset value, ReportInterval interval)
    {
        return interval switch
        {
            ReportInterval.Week => value.Date.AddDays(-(int)value.DayOfWeek),
            _ => value.Date
        };
    }
}

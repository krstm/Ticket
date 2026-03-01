using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Ticket.Data;
using Ticket.Data.Querying;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using SortDirection = Ticket.Domain.Enums.SortDirection;
using TicketSortBy = Ticket.Domain.Enums.TicketSortBy;

namespace Ticket.Services.Infrastructure;

internal sealed class TicketSearchPipeline
{
    private readonly ApplicationDbContext _context;
    private readonly TicketQueryParameters _parameters;
    private readonly int _maxPageSize;

    public TicketSearchPipeline(ApplicationDbContext context, TicketQueryParameters parameters, int maxPageSize)
    {
        _context = context;
        _parameters = parameters;
        _maxPageSize = maxPageSize;
    }

    public async Task<PagedResult<TicketSummaryDto>> ExecuteAsync(CancellationToken ct)
    {
        var preparedQuery = _context.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Department)
            .ApplyFilters(_parameters)
            .ApplySorting(_parameters);

        var pageSize = Math.Clamp(_parameters.PageSize, 1, _maxPageSize);
        var token = _parameters.PageToken;

        if (!string.IsNullOrWhiteSpace(token))
        {
            EnsureKeysetSort();
            var marker = ParsePageToken(token);

            var rawItems = await preparedQuery
                .Where(t => t.CreatedAtUtc <= marker.CreatedAtUtc)
                .ProjectToSummary()
                .Take(pageSize + marker.ServedAtTimestamp + 1)
                .ToListAsync(ct);

            var sliced = SkipPreviouslyServed(rawItems, marker);
            var trimmed = sliced.Take(pageSize + 1).ToList();
            var finalItems = trimmed.Take(pageSize).ToList();
            var nextToken = BuildNextToken(finalItems, trimmed.Count > pageSize, marker);

            return new PagedResult<TicketSummaryDto>
            {
                Items = finalItems,
                Page = 1,
                PageSize = pageSize,
                TotalCount = null,
                NextPageToken = nextToken
            };
        }

        var page = Math.Max(1, _parameters.Page);
        var skip = (page - 1) * pageSize;
        var total = await preparedQuery.LongCountAsync(ct);
        var results = await preparedQuery
            .ProjectToSummary()
            .Skip(skip)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var final = results.Take(pageSize).ToList();
        var next = BuildNextTokenForOffset(final, results.Count > pageSize);

        return new PagedResult<TicketSummaryDto>
        {
            Items = final,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            NextPageToken = next
        };
    }

    private void EnsureKeysetSort()
    {
        if (_parameters.SortBy != TicketSortBy.CreatedAt || _parameters.SortDirection != SortDirection.Desc)
        {
            throw new Exceptions.BadRequestException("Page tokens are only supported when sorting by CreatedAt descending.");
        }
    }

    private static TicketPageMarker ParsePageToken(string token)
    {
        if (!TicketPageToken.TryParse(token, out var marker))
        {
            throw new Exceptions.BadRequestException("Invalid page token.");
        }

        return marker;
    }

    private static List<TicketSummaryDto> SkipPreviouslyServed(List<TicketSummaryDto> items, TicketPageMarker marker)
    {
        if (marker.ServedAtTimestamp <= 0)
        {
            return items;
        }

        var skipped = 0;
        var filtered = new List<TicketSummaryDto>(items.Count);
        foreach (var item in items)
        {
            if (item.CreatedAtUtc == marker.CreatedAtUtc && skipped < marker.ServedAtTimestamp)
            {
                skipped++;
                continue;
            }

            filtered.Add(item);
        }

        return filtered;
    }

    private static string? BuildNextTokenForOffset(IReadOnlyList<TicketSummaryDto> items, bool hasExtra)
    {
        if (!hasExtra || items.Count == 0)
        {
            return null;
        }

        var last = items[^1];
        var servedAtTimestamp = items.Count(t => t.CreatedAtUtc == last.CreatedAtUtc);
        return TicketPageToken.Encode(last.Id, last.CreatedAtUtc, servedAtTimestamp);
    }

    private static string? BuildNextToken(IReadOnlyList<TicketSummaryDto> items, bool hasExtra, TicketPageMarker previousMarker)
    {
        if (!hasExtra || items.Count == 0)
        {
            return null;
        }

        var last = items[^1];
        var servedAtTimestamp = items.Count(t => t.CreatedAtUtc == last.CreatedAtUtc);
        var totalServed = last.CreatedAtUtc == previousMarker.CreatedAtUtc
            ? previousMarker.ServedAtTimestamp + servedAtTimestamp
            : servedAtTimestamp;

        return TicketPageToken.Encode(last.Id, last.CreatedAtUtc, totalServed);
    }
}

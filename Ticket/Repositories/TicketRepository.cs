using Microsoft.EntityFrameworkCore;
using Ticket.Data;
using Ticket.Domain.Enums;
using Ticket.DTOs.Requests;
using Ticket.Interfaces.Repositories;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly ApplicationDbContext _context;

    public TicketRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyCollection<TicketEntity> Items, long TotalCount)> SearchAsync(TicketQueryParameters query, CancellationToken ct)
    {
        var baseQuery = _context.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.History)
            .AsQueryable();

        baseQuery = ApplyFilters(baseQuery, query);
        var total = await baseQuery.LongCountAsync(ct);

        baseQuery = ApplySorting(baseQuery, query);

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var items = await baseQuery
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<TicketEntity?> GetByIdAsync(Guid id, bool includeHistory, bool asTracking, CancellationToken ct)
    {
        var query = asTracking
            ? _context.Tickets
            : _context.Tickets.AsNoTracking();

        query = query.Include(t => t.Category);

        if (includeHistory)
        {
            query = query.Include(t => t.History);
        }

        return query.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task AddAsync(TicketEntity ticket, CancellationToken ct)
    {
        await _context.Tickets.AddAsync(ticket, ct);
    }

    public Task UpdateAsync(TicketEntity ticket, CancellationToken ct)
    {
        _context.Tickets.Update(ticket);
        return Task.CompletedTask;
    }

    public Task<bool> AnyInCategoryAsync(int categoryId, CancellationToken ct)
    {
        return _context.Tickets.IgnoreQueryFilters().AnyAsync(t => t.CategoryId == categoryId, ct);
    }

    private static IQueryable<TicketEntity> ApplyFilters(IQueryable<TicketEntity> query, TicketQueryParameters parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Title.ToLower().Contains(term) ||
                t.Description.ToLower().Contains(term) ||
                (t.Requester.Name != null && t.Requester.Name.ToLower().Contains(term)) ||
                (t.Requester.Email != null && t.Requester.Email.ToLower().Contains(term)) ||
                (t.Recipient.Name != null && t.Recipient.Name.ToLower().Contains(term)) ||
                (t.Recipient.Email != null && t.Recipient.Email.ToLower().Contains(term)) ||
                (t.Category != null && t.Category.Name.ToLower().Contains(term)) ||
                (t.ReferenceCode != null && t.ReferenceCode.ToLower().Contains(term)));
        }

        if (parameters.CategoryIds?.Count > 0)
        {
            query = query.Where(t => parameters.CategoryIds.Contains(t.CategoryId));
        }

        if (parameters.Statuses?.Count > 0)
        {
            query = query.Where(t => parameters.Statuses.Contains(t.Status));
        }

        if (parameters.Priorities?.Count > 0)
        {
            query = query.Where(t => parameters.Priorities.Contains(t.Priority));
        }

        if (parameters.CreatedFrom.HasValue)
        {
            query = query.Where(t => t.CreatedAtUtc >= parameters.CreatedFrom.Value);
        }

        if (parameters.CreatedTo.HasValue)
        {
            query = query.Where(t => t.CreatedAtUtc <= parameters.CreatedTo.Value);
        }

        if (parameters.DueFrom.HasValue)
        {
            query = query.Where(t => t.DueAtUtc >= parameters.DueFrom.Value);
        }

        if (parameters.DueTo.HasValue)
        {
            query = query.Where(t => t.DueAtUtc <= parameters.DueTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Requester))
        {
            var requester = parameters.Requester.Trim().ToLowerInvariant();
            query = query.Where(t =>
                (t.Requester.Name != null && t.Requester.Name.ToLower().Contains(requester)) ||
                (t.Requester.Email != null && t.Requester.Email.ToLower().Contains(requester)));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Recipient))
        {
            var recipient = parameters.Recipient.Trim().ToLowerInvariant();
            query = query.Where(t =>
                (t.Recipient.Name != null && t.Recipient.Name.ToLower().Contains(recipient)) ||
                (t.Recipient.Email != null && t.Recipient.Email.ToLower().Contains(recipient)));
        }

        return query;
    }

    private static IQueryable<TicketEntity> ApplySorting(IQueryable<TicketEntity> query, TicketQueryParameters parameters)
    {
        var ascending = parameters.SortDirection == SortDirection.Asc;

        return parameters.SortBy switch
        {
            TicketSortBy.Priority => ascending
                ? query.OrderBy(t => t.Priority).ThenByDescending(t => t.CreatedAtUtc)
                : query.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAtUtc),

            TicketSortBy.Status => ascending
                ? query.OrderBy(t => t.Status).ThenByDescending(t => t.CreatedAtUtc)
                : query.OrderByDescending(t => t.Status).ThenByDescending(t => t.CreatedAtUtc),

            TicketSortBy.CategoryName => ascending
                ? query.OrderBy(t => t.Category != null ? t.Category.Name : string.Empty).ThenByDescending(t => t.CreatedAtUtc)
                : query.OrderByDescending(t => t.Category != null ? t.Category.Name : string.Empty).ThenByDescending(t => t.CreatedAtUtc),

            TicketSortBy.DueAt => ascending
                ? query.OrderBy(t => t.DueAtUtc.HasValue).ThenBy(t => t.DueAtUtc.HasValue ? t.DueAtUtc.Value.Ticks : long.MaxValue)
                : query.OrderByDescending(t => t.DueAtUtc.HasValue).ThenByDescending(t => t.DueAtUtc.HasValue ? t.DueAtUtc.Value.Ticks : long.MinValue),

            _ => ascending
                ? query.OrderBy(t => t.CreatedAtUtc.Ticks)
                : query.OrderByDescending(t => t.CreatedAtUtc.Ticks)
        };
    }
}

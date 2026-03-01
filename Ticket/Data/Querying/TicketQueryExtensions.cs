using Microsoft.EntityFrameworkCore;
using Ticket.Domain.Enums;
using Ticket.Domain.Support;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Data.Querying;

public static class TicketQueryExtensions
{
    public static IQueryable<TicketEntity> ApplyFilters(this IQueryable<TicketEntity> query, TicketQueryParameters parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var normalized = SearchNormalizer.NormalizeRequired(parameters.SearchTerm);
            var like = $"%{normalized}%";
            var fullContent = parameters.SearchScope != TicketSearchScope.TitleOnly;

            query = query.Where(t =>
                EF.Functions.Like(t.TitleNormalized, like) ||
                (fullContent && (
                    EF.Functions.Like(t.DescriptionNormalized, like) ||
                    (t.RequesterNameNormalized != null && EF.Functions.Like(t.RequesterNameNormalized, like)) ||
                    (t.RequesterEmailNormalized != null && EF.Functions.Like(t.RequesterEmailNormalized, like)) ||
                    (t.RecipientNameNormalized != null && EF.Functions.Like(t.RecipientNameNormalized, like)) ||
                    (t.RecipientEmailNormalized != null && EF.Functions.Like(t.RecipientEmailNormalized, like)) ||
                    (t.ReferenceCodeNormalized != null && EF.Functions.Like(t.ReferenceCodeNormalized, like)))));
        }

        if (parameters.CategoryIds?.Count > 0)
        {
            query = query.Where(t => parameters.CategoryIds.Contains(t.CategoryId));
        }

        if (parameters.DepartmentIds?.Count > 0)
        {
            query = query.Where(t => parameters.DepartmentIds.Contains(t.DepartmentId));
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
            var normalized = SearchNormalizer.NormalizeRequired(parameters.Requester);
            var like = $"%{normalized}%";
            query = query.Where(t =>
                (t.RequesterNameNormalized != null && EF.Functions.Like(t.RequesterNameNormalized, like)) ||
                (t.RequesterEmailNormalized != null && EF.Functions.Like(t.RequesterEmailNormalized, like)));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Recipient))
        {
            var normalized = SearchNormalizer.NormalizeRequired(parameters.Recipient);
            var like = $"%{normalized}%";
            query = query.Where(t =>
                (t.RecipientNameNormalized != null && EF.Functions.Like(t.RecipientNameNormalized, like)) ||
                (t.RecipientEmailNormalized != null && EF.Functions.Like(t.RecipientEmailNormalized, like)));
        }

        if (!string.IsNullOrWhiteSpace(parameters.DepartmentName))
        {
            var normalized = SearchNormalizer.NormalizeRequired(parameters.DepartmentName);
            var like = $"%{normalized}%";
            query = query.Where(t => EF.Functions.Like(t.DepartmentNameNormalized, like));
        }

        return query;
    }

    public static IQueryable<TicketEntity> ApplySorting(this IQueryable<TicketEntity> query, TicketQueryParameters parameters)
    {
        var ascending = parameters.SortDirection == SortDirection.Asc;

        return parameters.SortBy switch
        {
            TicketSortBy.Priority => ascending
                ? query.OrderBy(t => t.Priority).ThenByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
                : query.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id),

            TicketSortBy.Status => ascending
                ? query.OrderBy(t => t.Status).ThenByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
                : query.OrderByDescending(t => t.Status).ThenByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id),

            TicketSortBy.CategoryName => ascending
                ? query.OrderBy(t => t.Category != null ? t.Category.Name : string.Empty).ThenByDescending(t => t.CreatedAtUtc)
                : query.OrderByDescending(t => t.Category != null ? t.Category.Name : string.Empty).ThenByDescending(t => t.CreatedAtUtc),

            TicketSortBy.DueAt => ascending
                ? query.OrderBy(t => t.DueAtUtc ?? DateTimeOffset.MaxValue).ThenBy(t => t.Id)
                : query.OrderByDescending(t => t.DueAtUtc ?? DateTimeOffset.MinValue).ThenByDescending(t => t.Id),

            _ => ascending
                ? query.OrderBy(t => t.CreatedAtUtc).ThenBy(t => t.Id)
                : query.OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
        };
    }

    public static IQueryable<TicketSummaryDto> ProjectToSummary(this IQueryable<TicketEntity> query)
    {
        return query.Select(t => new TicketSummaryDto
        {
            Id = t.Id,
            Title = t.Title,
            Status = t.Status,
            Priority = t.Priority,
            CategoryId = t.CategoryId,
            CategoryName = t.Category != null ? t.Category.Name : string.Empty,
            DepartmentId = t.DepartmentId,
            DepartmentName = t.Department != null ? t.Department.Name : string.Empty,
            CreatedAtUtc = t.CreatedAtUtc,
            DueAtUtc = t.DueAtUtc,
            ReferenceCode = t.ReferenceCode,
            RowVersion = t.RowVersion
        });
    }
}

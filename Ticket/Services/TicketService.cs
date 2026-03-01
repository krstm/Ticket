using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticket.Data;
using Ticket.Data.Querying;
using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using Ticket.Domain.Events;
using Ticket.Domain.Rules;
using Ticket.Domain.Support;
using Ticket.Domain.ValueObjects;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Exceptions;
using Ticket.Interfaces.Infrastructure;
using Ticket.Interfaces.Services;
using Ticket.Services.Infrastructure;

namespace Ticket.Services;

public class TicketService : ITicketService
{
    private const int MaxPageSize = 100;
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IClock _clock;
    private readonly IContentSanitizer _contentSanitizer;
    private readonly ILogger<TicketService> _logger;
    private readonly TicketAccessEvaluator _accessEvaluator;

    public TicketService(
        ApplicationDbContext context,
        IMapper mapper,
        IClock clock,
        IContentSanitizer contentSanitizer,
        ILogger<TicketService> logger,
        TicketAccessEvaluator accessEvaluator)
    {
        _context = context;
        _mapper = mapper;
        _clock = clock;
        _contentSanitizer = contentSanitizer;
        _logger = logger;
        _accessEvaluator = accessEvaluator;
    }

    public async Task<PagedResult<TicketSummaryDto>> SearchAsync(TicketQueryParameters query, CancellationToken ct)
    {
        var preparedQuery = _context.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Department)
            .ApplyFilters(query)
            .ApplySorting(query);

        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var token = query.PageToken;

        if (!string.IsNullOrWhiteSpace(token))
        {
            EnsureKeysetSort(query);
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
        else
        {
            var page = Math.Max(1, query.Page);
            var skip = (page - 1) * pageSize;
            var total = await preparedQuery.LongCountAsync(ct);
            var results = await preparedQuery
                .ProjectToSummary()
                .Skip(skip)
                .Take(pageSize + 1)
                .ToListAsync(ct);

            var finalItems = results.Take(pageSize).ToList();
            var nextToken = BuildNextTokenForOffset(finalItems, results.Count > pageSize);

            return new PagedResult<TicketSummaryDto>
            {
                Items = finalItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                NextPageToken = nextToken
            };
        }
    }

    public async Task<TicketDetailsDto> GetAsync(Guid id, CancellationToken ct)
    {
        var ticket = await LoadDetailsAsync(id, ct);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket {id} not found.");
        }

        return _mapper.Map<TicketDetailsDto>(ticket);
    }

    public async Task<TicketDetailsDto> CreateAsync(TicketCreateRequest request, CancellationToken ct)
    {
        var category = await EnsureCategoryExists(request.CategoryId, ct);
        var department = await EnsureDepartmentExists(request.DepartmentId, ct, includeMembers: true);
        var createdBy = request.Requester?.Name?.Trim() ?? "system";

        var ticket = new Ticket.Domain.Entities.Ticket
        {
            Title = request.Title.Trim(),
            Description = SanitizeDescription(request.Description),
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Priority = request.Priority,
            Status = TicketStatus.New,
            DueAtUtc = request.DueAtUtc,
            ReferenceCode = string.IsNullOrWhiteSpace(request.ReferenceCode) ? null : request.ReferenceCode.Trim(),
            Requester = MapContact(request.Requester),
            Recipient = MapContact(request.Recipient),
            Metadata = MapMetadata(request.Metadata),
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow
        };

        ApplyNormalization(ticket, department.Name);

        await _context.Tickets.AddAsync(ticket, ct);
        ticket.AddDomainEvent(new TicketCreatedEvent(
            ticket,
            createdBy,
            ticket.ReferenceCode,
            _clock.UtcNow,
            department,
            GetActiveDepartmentMembers(department)));
        await _context.SaveChangesAsync(ct);

        var hydrated = await LoadDetailsAsync(ticket.Id, ct) ?? ticket;
        _logger.LogInformation("Ticket {TicketId} created in category {CategoryId}", ticket.Id, category.Id);
        return _mapper.Map<TicketDetailsDto>(hydrated);
    }

    public async Task<TicketDetailsDto> UpdateAsync(Guid id, TicketUpdateRequest request, byte[] rowVersion, CancellationToken ct)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Department)!.ThenInclude(d => d.Members)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (ticket is null)
        {
            throw new NotFoundException($"Ticket {id} not found.");
        }

        EnsureRowVersion(ticket.RowVersion, rowVersion);
        await EnsureCategoryExists(request.CategoryId, ct);
        var actor = MapActor(request.Actor);
        _accessEvaluator.EnsureParticipant(ticket, actor);

        var sanitizedDescription = SanitizeDescription(request.Description);
        ticket.Title = request.Title.Trim();
        if (!string.Equals(ticket.Description, sanitizedDescription, StringComparison.Ordinal))
        {
            _accessEvaluator.EnsureCanModifyDescription(actor);
            ticket.Description = sanitizedDescription;
        }
        ticket.Priority = request.Priority;
        ticket.CategoryId = request.CategoryId;
        if (ticket.DepartmentId != request.DepartmentId)
        {
            var department = await EnsureDepartmentExists(request.DepartmentId, ct, includeMembers: true, asTracking: true);
            ticket.DepartmentId = department.Id;
            ticket.Department = department;
        }
        ticket.DueAtUtc = request.DueAtUtc;
        ticket.ReferenceCode = string.IsNullOrWhiteSpace(request.ReferenceCode) ? null : request.ReferenceCode.Trim();
        ticket.Requester = MapContact(request.Requester);
        ticket.Recipient = MapContact(request.Recipient);
        ticket.Metadata = MapMetadata(request.Metadata);
        ticket.UpdatedAtUtc = _clock.UtcNow;

        ApplyNormalization(ticket, ticket.Department?.Name);

        await _context.SaveChangesAsync(ct);
        var refreshed = await LoadDetailsAsync(id, ct) ?? ticket;
        return _mapper.Map<TicketDetailsDto>(refreshed);
    }

    public async Task<TicketDetailsDto> UpdateStatusAsync(Guid id, TicketStatusUpdateRequest request, byte[] rowVersion, CancellationToken ct)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Department)!.ThenInclude(d => d.Members)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket {id} not found.");
        }

        EnsureRowVersion(ticket.RowVersion, rowVersion);
        var actor = MapActor(request.Actor);
        _accessEvaluator.EnsureParticipant(ticket, actor);

        if (!TicketStatusTransitionRules.CanTransition(ticket.Status, request.Status))
        {
            throw new BadRequestException($"Transition from {ticket.Status} to {request.Status} is not allowed.");
        }

        var previousStatus = ticket.Status;
        var note = SanitizeOptional(request.Note);

        ticket.Status = request.Status;
        ticket.UpdatedAtUtc = _clock.UtcNow;

        var changedBy = string.IsNullOrWhiteSpace(request.ChangedBy) ? actor.DisplayName : request.ChangedBy.Trim();
        var changedByEmail = actor.Email.Trim();
        var occurredAt = _clock.UtcNow;
        ticket.AddDomainEvent(new TicketStatusChangedEvent(
            ticket,
            previousStatus,
            request.Status,
            changedBy,
            changedByEmail,
            note,
            occurredAt,
            ticket.Department!,
            GetActiveDepartmentMembers(ticket.Department!)));
        if (request.Status == TicketStatus.Resolved)
        {
            ticket.AddDomainEvent(new TicketResolvedEvent(
                ticket,
                changedBy,
                note,
                occurredAt,
                changedByEmail,
                ticket.Department!,
                GetActiveDepartmentMembers(ticket.Department!)));
        }

        await _context.SaveChangesAsync(ct);

        var refreshed = await LoadDetailsAsync(id, ct) ?? ticket;
        return _mapper.Map<TicketDetailsDto>(refreshed);
    }

    public async Task<TicketCommentDto> AddCommentAsync(Guid id, TicketCommentCreateRequest request, CancellationToken ct)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Department)!.ThenInclude(d => d.Members)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Ticket {id} not found.");

        var actor = MapActor(request.Actor);
        _accessEvaluator.EnsureParticipant(ticket, actor);

        var sanitized = SanitizeComment(request.Body);

        var comment = new TicketComment
        {
            TicketId = ticket.Id,
            Body = sanitized,
            AuthorDisplayName = string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.Email : actor.DisplayName,
            AuthorEmail = actor.Email.Trim(),
            AuthorEmailNormalized = actor.NormalizedEmail,
            Source = actor.ActorType == TicketActorType.Requester ? TicketCommentSource.Requester : TicketCommentSource.DepartmentMember,
            CreatedAtUtc = _clock.UtcNow
        };

        ticket.Comments.Add(comment);
        ticket.LastCommentAtUtc = comment.CreatedAtUtc;
        ticket.UpdatedAtUtc = _clock.UtcNow;

        ticket.AddDomainEvent(new TicketCommentAddedEvent(
            ticket,
            comment,
            comment.Source,
            comment.AuthorEmail,
            comment.CreatedAtUtc,
            ticket.Department!,
            GetActiveDepartmentMembers(ticket.Department!)));

        await _context.SaveChangesAsync(ct);
        return _mapper.Map<TicketCommentDto>(comment);
    }

    public async Task<IReadOnlyCollection<TicketCommentDto>> GetCommentsAsync(Guid id, CancellationToken ct)
    {
        var exists = await _context.Tickets.AsNoTracking().AnyAsync(t => t.Id == id, ct);
        if (!exists)
        {
            throw new NotFoundException($"Ticket {id} not found.");
        }

        var comments = await _context.TicketComments
            .AsNoTracking()
            .Where(c => c.TicketId == id)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        return _mapper.Map<IReadOnlyCollection<TicketCommentDto>>(comments);
    }

    private async Task<Category> EnsureCategoryExists(int categoryId, CancellationToken ct)
    {
        var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, ct);
        if (category is null || !category.IsActive)
        {
            throw new NotFoundException($"Category {categoryId} not found or inactive.");
        }

        return category;
    }

    private async Task<Department> EnsureDepartmentExists(int departmentId, CancellationToken ct, bool includeMembers = false, bool asTracking = false)
    {
        IQueryable<Department> query = asTracking ? _context.Departments : _context.Departments.AsNoTracking();
        if (includeMembers)
        {
            query = query.Include(d => d.Members);
        }

        var department = await query.FirstOrDefaultAsync(d => d.Id == departmentId, ct);
        if (department is null || !department.IsActive)
        {
            throw new NotFoundException($"Department {departmentId} not found or inactive.");
        }

        return department;
    }

    private Task<Ticket.Domain.Entities.Ticket?> LoadDetailsAsync(Guid id, CancellationToken ct)
    {
        return _context.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Department)!.ThenInclude(d => d.Members)
            .Include(t => t.History)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    private static void EnsureRowVersion(byte[] current, byte[] provided)
    {
        if (provided is null || provided.Length == 0)
        {
            throw new BadRequestException("RowVersion (If-Match header) is required.");
        }

        if (!current.SequenceEqual(provided))
        {
            throw new ConflictException("Ticket was updated by another process.");
        }
    }

    private string SanitizeDescription(string description)
    {
        var sanitized = _contentSanitizer.Sanitize(description);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new BadRequestException("Description becomes empty after sanitization.");
        }

        return sanitized;
    }

    private string? SanitizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = _contentSanitizer.Sanitize(value);
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    private string SanitizeComment(string body)
    {
        var sanitized = _contentSanitizer.Sanitize(body);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new BadRequestException("Comment body cannot be empty after sanitization.");
        }

        return sanitized;
    }

    private static TicketContactInfo MapContact(TicketContactInfoDto? dto)
    {
        if (dto is null)
        {
            return TicketContactInfo.Empty;
        }

        return new TicketContactInfo(
            string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim(),
            string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim());
    }

    private static TicketMetadata MapMetadata(TicketMetadataDto? dto)
    {
        if (dto is null)
        {
            return TicketMetadata.Empty;
        }

        return new TicketMetadata(dto.IsExternal, dto.RequiresFollowUp);
    }

    private static TicketActorContext MapActor(TicketActorContextDto? dto)
    {
        if (dto is null)
        {
            throw new BadRequestException("Actor context is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            throw new BadRequestException("Actor email is required.");
        }

        var email = dto.Email.Trim();
        var name = string.IsNullOrWhiteSpace(dto.Name) ? email : dto.Name.Trim();
        return new TicketActorContext(name, email, dto.ActorType);
    }

    private static void ApplyNormalization(Ticket.Domain.Entities.Ticket ticket, string? departmentName = null)
    {
        ticket.TitleNormalized = SearchNormalizer.NormalizeRequired(ticket.Title);
        ticket.DescriptionNormalized = SearchNormalizer.NormalizeRequired(ticket.Description);
        ticket.DepartmentNameNormalized = SearchNormalizer.NormalizeRequired(departmentName ?? ticket.Department?.Name);
        ticket.RequesterNameNormalized = SearchNormalizer.NormalizeOptional(ticket.Requester.Name);
        ticket.RequesterEmailNormalized = SearchNormalizer.NormalizeOptional(ticket.Requester.Email);
        ticket.RecipientNameNormalized = SearchNormalizer.NormalizeOptional(ticket.Recipient.Name);
        ticket.RecipientEmailNormalized = SearchNormalizer.NormalizeOptional(ticket.Recipient.Email);
        ticket.ReferenceCodeNormalized = SearchNormalizer.NormalizeOptional(ticket.ReferenceCode);
    }

    private static TicketPageMarker ParsePageToken(string token)
    {
        if (!TicketPageToken.TryParse(token, out var marker))
        {
            throw new BadRequestException("Invalid page token.");
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

    private static void EnsureKeysetSort(TicketQueryParameters parameters)
    {
        if (parameters.SortBy != TicketSortBy.CreatedAt || parameters.SortDirection != SortDirection.Desc)
        {
            throw new BadRequestException("Page tokens are only supported when sorting by CreatedAt descending.");
        }
    }

    private static IReadOnlyCollection<DepartmentMember> GetActiveDepartmentMembers(Department department)
    {
        return department.Members
            .Where(m => m.IsActive)
            .ToList();
    }
}

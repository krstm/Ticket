using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticket.Data;
using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
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
using Ticket.Services.Mapping;

namespace Ticket.Services;

public class TicketService : ITicketService
{
    private const int MaxPageSize = 100;
    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;
    private readonly IContentSanitizer _contentSanitizer;
    private readonly ILogger<TicketService> _logger;
    private readonly TicketAccessEvaluator _accessEvaluator;

    public TicketService(
        ApplicationDbContext context,
        IClock clock,
        IContentSanitizer contentSanitizer,
        ILogger<TicketService> logger,
        TicketAccessEvaluator accessEvaluator)
    {
        _context = context;
        _clock = clock;
        _contentSanitizer = contentSanitizer;
        _logger = logger;
        _accessEvaluator = accessEvaluator;
    }

    public async Task<PagedResult<TicketSummaryDto>> SearchAsync(TicketQueryParameters query, CancellationToken ct)
    {
        var pipeline = new TicketSearchPipeline(_context, query, MaxPageSize);
        return await pipeline.ExecuteAsync(ct);
    }

    public async Task<TicketDetailsDto> GetAsync(Guid id, CancellationToken ct)
    {
        var ticket = await LoadDetailsAsync(id, ct);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket {id} not found.");
        }

        return ticket.ToDetails();
    }

    public async Task<TicketDetailsDto> CreateAsync(TicketCreateRequest request, CancellationToken ct)
    {
        var category = await EnsureCategoryExists(request.CategoryId, ct);
        var department = await EnsureDepartmentExists(request.DepartmentId, ct, includeMembers: true);
        var createdBy = request.Requester?.Name?.Trim() ?? "system";
        var sanitizedDescription = TicketMutationHelper.SanitizeRequired(_contentSanitizer, request.Description, "Description becomes empty after sanitization.");

        var ticket = new Ticket.Domain.Entities.Ticket
        {
            Title = request.Title.Trim(),
            Description = sanitizedDescription,
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

        TicketMutationHelper.ApplyNormalization(ticket, department.Name);

        await _context.Tickets.AddAsync(ticket, ct);
        ticket.AddDomainEvent(TicketDomainEventFactory.Created(
            ticket,
            createdBy,
            ticket.ReferenceCode,
            _clock.UtcNow,
            department));
        await _context.SaveChangesAsync(ct);

        var hydrated = await LoadDetailsAsync(ticket.Id, ct) ?? ticket;
        _logger.LogInformation("Ticket {TicketId} created in category {CategoryId}", ticket.Id, category.Id);
        return hydrated.ToDetails();
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

        TicketMutationHelper.EnsureRowVersion(ticket.RowVersion, rowVersion);
        await EnsureCategoryExists(request.CategoryId, ct);
        var actor = MapActor(request.Actor);
        _accessEvaluator.EnsureParticipant(ticket, actor);

        var sanitizedDescription = TicketMutationHelper.SanitizeRequired(_contentSanitizer, request.Description, "Description becomes empty after sanitization.");
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

        TicketMutationHelper.ApplyNormalization(ticket, ticket.Department?.Name);

        await _context.SaveChangesAsync(ct);
        var refreshed = await LoadDetailsAsync(id, ct) ?? ticket;
        return refreshed.ToDetails();
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

        TicketMutationHelper.EnsureRowVersion(ticket.RowVersion, rowVersion);
        var actor = MapActor(request.Actor);
        _accessEvaluator.EnsureParticipant(ticket, actor);

        if (!TicketStatusTransitionRules.CanTransition(ticket.Status, request.Status))
        {
            throw new BadRequestException($"Transition from {ticket.Status} to {request.Status} is not allowed.");
        }

        var previousStatus = ticket.Status;
        var note = TicketMutationHelper.SanitizeOptional(_contentSanitizer, request.Note);

        ticket.Status = request.Status;
        ticket.UpdatedAtUtc = _clock.UtcNow;

        var occurredAt = _clock.UtcNow;
        ticket.AddDomainEvent(TicketDomainEventFactory.StatusChanged(
            ticket,
            previousStatus,
            request.Status,
            actor,
            note,
            occurredAt,
            ticket.Department!));
        if (request.Status == TicketStatus.Resolved)
        {
            ticket.AddDomainEvent(TicketDomainEventFactory.Resolved(
                ticket,
                actor,
                note,
                occurredAt,
                ticket.Department!));
        }

        await _context.SaveChangesAsync(ct);

        var refreshed = await LoadDetailsAsync(id, ct) ?? ticket;
        return refreshed.ToDetails();
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

        var comment = TicketCommentHelper.CreateComment(ticket, actor, request.Body, _contentSanitizer, _clock);

        ticket.Comments.Add(comment);
        ticket.LastCommentAtUtc = comment.CreatedAtUtc;
        ticket.UpdatedAtUtc = _clock.UtcNow;

        ticket.AddDomainEvent(TicketDomainEventFactory.CommentAdded(
            ticket,
            comment,
            ticket.Department!));

        await _context.SaveChangesAsync(ct);
        return comment.ToDto();
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

        return comments.Select(c => c.ToDto()).ToArray();
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

}

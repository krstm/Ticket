using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticket.Domain.Entities;
using Ticket.Domain.Enums;
using Ticket.Domain.Rules;
using Ticket.Domain.ValueObjects;
using Ticket.DTOs.Common;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Exceptions;
using Ticket.Interfaces.Infrastructure;
using Ticket.Interfaces.Repositories;
using Ticket.Interfaces.Services;

namespace Ticket.Services;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly IMapper _mapper;
    private readonly IClock _clock;
    private readonly IContentSanitizer _contentSanitizer;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        ITicketRepository ticketRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        IMapper mapper,
        IClock clock,
        IContentSanitizer contentSanitizer,
        ILogger<TicketService> logger)
    {
        _ticketRepository = ticketRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _mapper = mapper;
        _clock = clock;
        _contentSanitizer = contentSanitizer;
        _logger = logger;
    }

    public async Task<PagedResult<TicketSummaryDto>> SearchAsync(TicketQueryParameters query, CancellationToken ct)
    {
        var (items, total) = await _ticketRepository.SearchAsync(query, ct);
        var mapped = _mapper.Map<IReadOnlyCollection<TicketSummaryDto>>(items);

        return new PagedResult<TicketSummaryDto>
        {
            Items = mapped,
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100),
            TotalCount = total
        };
    }

    public async Task<TicketDetailsDto> GetAsync(Guid id, CancellationToken ct)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, includeHistory: true, asTracking: false, ct);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket {id} not found.");
        }

        return _mapper.Map<TicketDetailsDto>(ticket);
    }

    public async Task<TicketDetailsDto> CreateAsync(TicketCreateRequest request, CancellationToken ct)
    {
        var category = await EnsureCategoryExists(request.CategoryId, ct);

        var ticket = new Ticket.Domain.Entities.Ticket
        {
            Title = request.Title.Trim(),
            Description = SanitizeDescription(request.Description),
            CategoryId = category.Id,
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

        ticket.History.Add(new TicketHistory
        {
            Action = "Ticket created",
            ChangedBy = "system",
            Note = request.ReferenceCode,
            OccurredAtUtc = _clock.UtcNow,
            Status = TicketStatus.New
        });

        await _ticketRepository.AddAsync(ticket, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.RecordAsync(ticket, "Ticket created", request.ReferenceCode, "system", ct);

        _logger.LogInformation("Ticket {TicketId} created in category {CategoryId}", ticket.Id, category.Id);

        var reloaded = await _ticketRepository.GetByIdAsync(ticket.Id, includeHistory: true, asTracking: false, ct)
            ?? ticket;

        return _mapper.Map<TicketDetailsDto>(reloaded);
    }

    public async Task<TicketDetailsDto> UpdateAsync(Guid id, TicketUpdateRequest request, byte[] rowVersion, CancellationToken ct)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, includeHistory: true, asTracking: true, ct);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket {id} not found.");
        }

        EnsureRowVersion(ticket.RowVersion, rowVersion);
        await EnsureCategoryExists(request.CategoryId, ct);

        ticket.Title = request.Title.Trim();
        ticket.Description = SanitizeDescription(request.Description);
        ticket.Priority = request.Priority;
        ticket.CategoryId = request.CategoryId;
        ticket.DueAtUtc = request.DueAtUtc;
        ticket.ReferenceCode = string.IsNullOrWhiteSpace(request.ReferenceCode) ? null : request.ReferenceCode.Trim();
        ticket.Requester = MapContact(request.Requester);
        ticket.Recipient = MapContact(request.Recipient);
        ticket.Metadata = MapMetadata(request.Metadata);
        ticket.UpdatedAtUtc = _clock.UtcNow;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.RecordAsync(ticket, "Ticket updated", request.ReferenceCode, "system", ct);

        var refreshed = await _ticketRepository.GetByIdAsync(id, includeHistory: true, asTracking: false, ct)
            ?? ticket;

        return _mapper.Map<TicketDetailsDto>(refreshed);
    }

    public async Task<TicketDetailsDto> UpdateStatusAsync(Guid id, TicketStatusUpdateRequest request, byte[] rowVersion, CancellationToken ct)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, includeHistory: true, asTracking: true, ct);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket {id} not found.");
        }

        EnsureRowVersion(ticket.RowVersion, rowVersion);

        if (!TicketStatusTransitionRules.CanTransition(ticket.Status, request.Status))
        {
            throw new BadRequestException($"Transition from {ticket.Status} to {request.Status} is not allowed.");
        }

        ticket.Status = request.Status;
        ticket.UpdatedAtUtc = _clock.UtcNow;
        ticket.History.Add(new TicketHistory
        {
            Status = request.Status,
            Action = $"Status changed to {request.Status}",
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ChangedBy = request.ChangedBy,
            OccurredAtUtc = _clock.UtcNow
        });

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.RecordAsync(ticket, $"Status changed to {request.Status}", request.Note, request.ChangedBy, ct);

        var refreshed = await _ticketRepository.GetByIdAsync(id, includeHistory: true, asTracking: false, ct)
            ?? ticket;

        return _mapper.Map<TicketDetailsDto>(refreshed);
    }

    private async Task<Category> EnsureCategoryExists(int categoryId, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, ct);
        if (category is null || !category.IsActive)
        {
            throw new NotFoundException($"Category {categoryId} not found or inactive.");
        }

        return category;
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
}

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticket.Data;
using Ticket.Domain.Entities;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Exceptions;
using Ticket.Interfaces.Infrastructure;
using Ticket.Interfaces.Services;

namespace Ticket.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IClock _clock;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        ApplicationDbContext context,
        IMapper mapper,
        IClock clock,
        ILogger<CategoryService> logger)
    {
        _context = context;
        _mapper = mapper;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _context.Categories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var categories = await query.OrderBy(c => c.Name).ToListAsync(ct);
        return _mapper.Map<IReadOnlyCollection<CategoryDto>>(categories);
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateRequest request, CancellationToken ct)
    {
        await EnsureUniqueName(request.Name, null, ct);

        var category = new Category
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
            IsActive = true
        };

        await _context.Categories.AddAsync(category, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Category {CategoryId} created", category.Id);
        return _mapper.Map<CategoryDto>(category);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryUpdateRequest request, CancellationToken ct)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Category {id} not found.");

        await EnsureUniqueName(request.Name, id, ct);

        category.Name = request.Name.Trim();
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(ct);
        return _mapper.Map<CategoryDto>(category);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Category {id} not found.");

        var hasTickets = await _context.Tickets.IgnoreQueryFilters().AnyAsync(t => t.CategoryId == id, ct);
        if (hasTickets)
        {
            throw new BadRequestException("Cannot deactivate category while tickets exist.");
        }

        category.IsActive = false;
        category.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(int id, CancellationToken ct)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Category {id} not found.");

        category.IsActive = true;
        category.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueName(string name, int? excludeId, CancellationToken ct)
    {
        var normalized = name.Trim().ToUpperInvariant();
        var query = _context.Categories.Where(c => c.Name.ToUpper() == normalized);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        if (await query.AnyAsync(ct))
        {
            throw new BadRequestException($"Category name '{name}' already exists.");
        }
    }
}

using AutoMapper;
using Microsoft.Extensions.Logging;
using Ticket.Domain.Entities;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Exceptions;
using Ticket.Interfaces.Repositories;
using Ticket.Interfaces.Services;

namespace Ticket.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        ICategoryRepository categoryRepository,
        ITicketRepository ticketRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CategoryService> logger)
    {
        _categoryRepository = categoryRepository;
        _ticketRepository = ticketRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var categories = await _categoryRepository.GetAllAsync(includeInactive, ct);
        return _mapper.Map<IReadOnlyCollection<CategoryDto>>(categories);
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateRequest request, CancellationToken ct)
    {
        await EnsureUniqueName(request.Name, null, ct);

        var category = new Category
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _categoryRepository.AddAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Category {CategoryId} created", category.Id);

        return _mapper.Map<CategoryDto>(category);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryUpdateRequest request, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Category {id} not found.");

        await EnsureUniqueName(request.Name, id, ct);

        category.Name = request.Name.Trim();
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _categoryRepository.UpdateAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return _mapper.Map<CategoryDto>(category);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Category {id} not found.");

        var hasTickets = await _ticketRepository.AnyInCategoryAsync(id, ct);
        if (hasTickets)
        {
            throw new BadRequestException("Cannot deactivate category while tickets exist.");
        }

        category.IsActive = false;
        category.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _categoryRepository.UpdateAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(int id, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Category {id} not found.");

        category.IsActive = true;
        category.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _categoryRepository.UpdateAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueName(string name, int? excludeId, CancellationToken ct)
    {
        if (await _categoryRepository.ExistsByNameAsync(name.Trim(), excludeId, ct))
        {
            throw new BadRequestException($"Category name '{name}' already exists.");
        }
    }
}

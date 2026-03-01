using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;

namespace Ticket.Interfaces.Services;

public interface ICategoryService
{
    Task<IReadOnlyCollection<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken ct);
    Task<CategoryDto> CreateAsync(CategoryCreateRequest request, CancellationToken ct);
    Task<CategoryDto> UpdateAsync(int id, CategoryUpdateRequest request, CancellationToken ct);
    Task DeactivateAsync(int id, CancellationToken ct);
    Task ReactivateAsync(int id, CancellationToken ct);
}

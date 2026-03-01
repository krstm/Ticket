using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;

namespace Ticket.Interfaces.Services;

public interface IDepartmentService
{
    Task<IReadOnlyCollection<DepartmentDto>> GetAllAsync(bool includeInactive, CancellationToken ct);
    Task<DepartmentDto> GetAsync(int id, CancellationToken ct);
    Task<DepartmentDto> CreateAsync(DepartmentCreateRequest request, CancellationToken ct);
    Task<DepartmentDto> UpdateAsync(int id, DepartmentUpdateRequest request, CancellationToken ct);
}

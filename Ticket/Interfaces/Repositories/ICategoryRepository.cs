using Ticket.Domain.Entities;

namespace Ticket.Interfaces.Repositories;

public interface ICategoryRepository
{
    Task<IReadOnlyCollection<Category>> GetAllAsync(bool includeInactive, CancellationToken ct);
    Task<Category?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken ct);
    Task AddAsync(Category category, CancellationToken ct);
    Task UpdateAsync(Category category, CancellationToken ct);
}

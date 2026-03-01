using Microsoft.EntityFrameworkCore;
using Ticket.Data;
using Ticket.Domain.Entities;
using Ticket.Interfaces.Repositories;

namespace Ticket.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Category>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _context.Categories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public Task<Category?> GetByIdAsync(int id, CancellationToken ct)
    {
        return _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var query = _context.Categories.Where(c => c.Name.ToLower() == normalized);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return query.AnyAsync(ct);
    }

    public async Task AddAsync(Category category, CancellationToken ct)
    {
        await _context.Categories.AddAsync(category, ct);
    }

    public Task UpdateAsync(Category category, CancellationToken ct)
    {
        _context.Categories.Update(category);
        return Task.CompletedTask;
    }
}

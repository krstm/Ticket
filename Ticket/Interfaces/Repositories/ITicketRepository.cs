using Ticket.DTOs.Requests;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Interfaces.Repositories;

public interface ITicketRepository
{
    Task<(IReadOnlyCollection<TicketEntity> Items, long TotalCount)> SearchAsync(TicketQueryParameters query, CancellationToken ct);
    Task<TicketEntity?> GetByIdAsync(Guid id, bool includeHistory, bool asTracking, CancellationToken ct);
    Task AddAsync(TicketEntity ticket, CancellationToken ct);
    Task UpdateAsync(TicketEntity ticket, CancellationToken ct);
    Task<bool> AnyInCategoryAsync(int categoryId, CancellationToken ct);
}

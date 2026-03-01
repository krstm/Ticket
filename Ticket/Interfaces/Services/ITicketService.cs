using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;

namespace Ticket.Interfaces.Services;

public interface ITicketService
{
    Task<PagedResult<TicketSummaryDto>> SearchAsync(TicketQueryParameters query, CancellationToken ct);
    Task<TicketDetailsDto> GetAsync(Guid id, CancellationToken ct);
    Task<TicketDetailsDto> CreateAsync(TicketCreateRequest request, CancellationToken ct);
    Task<TicketDetailsDto> UpdateAsync(Guid id, TicketUpdateRequest request, byte[] rowVersion, CancellationToken ct);
    Task<TicketDetailsDto> UpdateStatusAsync(Guid id, TicketStatusUpdateRequest request, byte[] rowVersion, CancellationToken ct);
}

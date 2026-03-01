using System.Linq;
using Ticket.Domain.Entities;
using Ticket.Domain.ValueObjects;
using Ticket.DTOs.Common;
using Ticket.DTOs.Responses;
using TicketEntity = Ticket.Domain.Entities.Ticket;
using TicketHistory = Ticket.Domain.Entities.TicketHistory;
using TicketComment = Ticket.Domain.Entities.TicketComment;

namespace Ticket.Services.Mapping;

public static class TicketMappingExtensions
{
    public static TicketSummaryDto ToSummary(this TicketEntity ticket) =>
        ApplySummary(ticket, new TicketSummaryDto());

    public static TicketDetailsDto ToDetails(this TicketEntity ticket)
    {
        var dto = ApplySummary(ticket, new TicketDetailsDto());
        dto.Description = ticket.Description;
        dto.Requester = ticket.Requester.ToDto();
        dto.Recipient = ticket.Recipient.ToDto();
        dto.Metadata = ticket.Metadata.ToDto();
        dto.UpdatedAtUtc = ticket.UpdatedAtUtc;

        dto.History = ticket.History
            .OrderByDescending(h => h.OccurredAtUtc)
            .Select(h => h.ToDto())
            .ToArray();

        dto.Department = ticket.Department != null
            ? ticket.Department.ToTicketDepartmentDto()
            : new TicketDepartmentDto { Id = ticket.DepartmentId, Name = string.Empty };

        dto.Comments = ticket.Comments
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => c.ToDto())
            .ToArray();

        return dto;
    }

    public static TicketHistoryDto ToDto(this TicketHistory history) => new()
    {
        Id = history.Id,
        Status = history.Status,
        Action = history.Action,
        Note = history.Note,
        ChangedBy = history.ChangedBy,
        OccurredAtUtc = history.OccurredAtUtc
    };

    public static TicketCommentDto ToDto(this TicketComment comment) => new()
    {
        Id = comment.Id,
        TicketId = comment.TicketId,
        Body = comment.Body,
        AuthorDisplayName = comment.AuthorDisplayName,
        AuthorEmail = comment.AuthorEmail,
        Source = comment.Source,
        CreatedAtUtc = comment.CreatedAtUtc
    };

    private static T ApplySummary<T>(TicketEntity ticket, T destination) where T : TicketSummaryDto
    {
        destination.Id = ticket.Id;
        destination.Title = ticket.Title;
        destination.Status = ticket.Status;
        destination.Priority = ticket.Priority;
        destination.CategoryId = ticket.CategoryId;
        destination.CategoryName = ticket.Category?.Name ?? string.Empty;
        destination.DepartmentId = ticket.DepartmentId;
        destination.DepartmentName = ticket.Department?.Name ?? string.Empty;
        destination.CreatedAtUtc = ticket.CreatedAtUtc;
        destination.DueAtUtc = ticket.DueAtUtc;
        destination.ReferenceCode = ticket.ReferenceCode;
        destination.RowVersion = ticket.RowVersion;
        return destination;
    }

    private static TicketContactInfoDto ToDto(this TicketContactInfo contact) => new()
    {
        Name = contact.Name,
        Email = contact.Email,
        Phone = contact.Phone
    };

    private static TicketMetadataDto ToDto(this TicketMetadata metadata) => new()
    {
        IsExternal = metadata.IsExternal,
        RequiresFollowUp = metadata.RequiresFollowUp,
        Channel = metadata.Channel
    };
}

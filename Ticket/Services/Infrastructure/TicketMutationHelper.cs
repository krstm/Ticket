using System.Linq;
using Ticket.Domain.Entities;
using Ticket.Domain.Support;
using Ticket.Exceptions;
using Ticket.Interfaces.Infrastructure;
using TicketEntity = Ticket.Domain.Entities.Ticket;

namespace Ticket.Services.Infrastructure;

public static class TicketMutationHelper
{
    public static string SanitizeRequired(IContentSanitizer sanitizer, string value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException(errorMessage);
        }

        var sanitized = sanitizer.Sanitize(value);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new BadRequestException(errorMessage);
        }

        return sanitized;
    }

    public static string? SanitizeOptional(IContentSanitizer sanitizer, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = sanitizer.Sanitize(value);
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    public static void EnsureRowVersion(byte[] current, byte[] provided)
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

    public static void ApplyNormalization(TicketEntity ticket, string? departmentName = null)
    {
        ticket.TitleNormalized = SearchNormalizer.NormalizeRequired(ticket.Title);
        ticket.DescriptionNormalized = SearchNormalizer.NormalizeRequired(ticket.Description);
        ticket.DepartmentNameNormalized = SearchNormalizer.NormalizeRequired(departmentName ?? ticket.Department?.Name);
        ticket.RequesterNameNormalized = SearchNormalizer.NormalizeOptional(ticket.Requester.Name);
        ticket.RequesterEmailNormalized = SearchNormalizer.NormalizeOptional(ticket.Requester.Email);
        ticket.RecipientNameNormalized = SearchNormalizer.NormalizeOptional(ticket.Recipient.Name);
        ticket.RecipientEmailNormalized = SearchNormalizer.NormalizeOptional(ticket.Recipient.Email);
        ticket.ReferenceCodeNormalized = SearchNormalizer.NormalizeOptional(ticket.ReferenceCode);
    }
}

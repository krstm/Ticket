using System.Collections.Generic;
using System.Linq;
using Ticket.Domain.Entities;
using Ticket.DTOs.Responses;

namespace Ticket.Services.Mapping;

public static class CategoryMappingExtensions
{
    public static CategoryDto ToDto(this Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        IsActive = category.IsActive,
        CreatedAtUtc = category.CreatedAtUtc,
        UpdatedAtUtc = category.UpdatedAtUtc
    };

    public static IReadOnlyCollection<CategoryDto> ToDtoList(this IEnumerable<Category> categories) =>
        categories.Select(c => c.ToDto()).ToArray();
}

using System.Collections.Generic;
using System.Linq;
using Ticket.Domain.Entities;
using Ticket.DTOs.Responses;

namespace Ticket.Services.Mapping;

public static class DepartmentMappingExtensions
{
    public static DepartmentDto ToDto(this Department department) => new()
    {
        Id = department.Id,
        Name = department.Name,
        Description = department.Description,
        IsActive = department.IsActive,
        CreatedAtUtc = department.CreatedAtUtc,
        UpdatedAtUtc = department.UpdatedAtUtc,
        Members = department.Members.Select(m => m.ToDto()).ToArray()
    };

    public static DepartmentMemberDto ToDto(this DepartmentMember member) => new()
    {
        Id = member.Id,
        FullName = member.FullName,
        Email = member.Email,
        IsActive = member.IsActive,
        NotifyOnTicketEmail = member.NotifyOnTicketEmail
    };

    public static TicketDepartmentDto ToTicketDepartmentDto(this Department department) => new()
    {
        Id = department.Id,
        Name = department.Name,
        Members = department.Members.Select(m => m.ToDto()).ToArray()
    };

    public static IReadOnlyCollection<DepartmentDto> ToDtoList(this IEnumerable<Department> departments) =>
        departments.Select(d => d.ToDto()).ToArray();
}

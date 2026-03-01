using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticket.Data;
using Ticket.Domain.Entities;
using Ticket.DTOs.Requests;
using Ticket.DTOs.Responses;
using Ticket.Exceptions;
using Ticket.Interfaces.Infrastructure;
using Ticket.Interfaces.Services;

namespace Ticket.Services;

public class DepartmentService : IDepartmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IClock _clock;
    private readonly ILogger<DepartmentService> _logger;

    public DepartmentService(ApplicationDbContext context, IMapper mapper, IClock clock, ILogger<DepartmentService> logger)
    {
        _context = context;
        _mapper = mapper;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<DepartmentDto>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _context.Departments
            .Include(d => d.Members)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        var departments = await query
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        return _mapper.Map<IReadOnlyCollection<DepartmentDto>>(departments);
    }

    public async Task<DepartmentDto> GetAsync(int id, CancellationToken ct)
    {
        var department = await _context.Departments
            .Include(d => d.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"Department {id} not found.");

        return _mapper.Map<DepartmentDto>(department);
    }

    public async Task<DepartmentDto> CreateAsync(DepartmentCreateRequest request, CancellationToken ct)
    {
        await EnsureUniqueNameAsync(request.Name, null, ct);

        var department = new Department
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow,
            IsActive = true
        };

        foreach (var memberRequest in request.Members)
        {
            department.Members.Add(MapMember(memberRequest));
        }

        await _context.Departments.AddAsync(department, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Department {DepartmentId} created with {MemberCount} members.", department.Id, department.Members.Count);
        return _mapper.Map<DepartmentDto>(department);
    }

    public async Task<DepartmentDto> UpdateAsync(int id, DepartmentUpdateRequest request, CancellationToken ct)
    {
        var department = await _context.Departments
            .Include(d => d.Members)
            .FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"Department {id} not found.");

        await EnsureUniqueNameAsync(request.Name, id, ct);

        department.Name = request.Name.Trim();
        department.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        department.IsActive = request.IsActive;
        department.UpdatedAtUtc = _clock.UtcNow;

        SyncMembers(department, request.Members);

        await _context.SaveChangesAsync(ct);
        return _mapper.Map<DepartmentDto>(department);
    }

    private async Task EnsureUniqueNameAsync(string name, int? excludeId, CancellationToken ct)
    {
        var normalized = name.Trim().ToUpperInvariant();
        var query = _context.Departments.Where(d => d.Name.ToUpper() == normalized);
        if (excludeId.HasValue)
        {
            query = query.Where(d => d.Id != excludeId.Value);
        }

        if (await query.AnyAsync(ct))
        {
            throw new BadRequestException($"Department name '{name}' already exists.");
        }
    }

    private static DepartmentMember MapMember(DepartmentMemberRequest request)
    {
        var email = request.Email.Trim();
        return new DepartmentMember
        {
            FullName = request.FullName.Trim(),
            Email = email,
            EmailNormalized = email.ToUpperInvariant(),
            IsActive = request.IsActive,
            NotifyOnTicketEmail = request.NotifyOnTicketEmail
        };
    }

    private static void SyncMembers(Department department, IReadOnlyCollection<DepartmentMemberRequest> requestedMembers)
    {
        var lookup = department.Members.ToDictionary(m => m.Id, m => m);

        foreach (var memberRequest in requestedMembers)
        {
            if (memberRequest.Id.HasValue && lookup.TryGetValue(memberRequest.Id.Value, out var existing))
            {
                var email = memberRequest.Email.Trim();
                existing.FullName = memberRequest.FullName.Trim();
                existing.Email = email;
                existing.EmailNormalized = email.ToUpperInvariant();
                existing.IsActive = memberRequest.IsActive;
                existing.NotifyOnTicketEmail = memberRequest.NotifyOnTicketEmail;
            }
            else
            {
                department.Members.Add(MapMember(memberRequest));
            }
        }

        var requestedIds = requestedMembers
            .Where(m => m.Id.HasValue)
            .Select(m => m.Id!.Value)
            .ToHashSet();

        var staleMembers = department.Members
            .Where(m => !requestedIds.Contains(m.Id))
            .ToList();

        foreach (var stale in staleMembers)
        {
            department.Members.Remove(stale);
        }
    }
}

using AutoMapper;
using Ticket.Domain.Entities;
using Ticket.Domain.ValueObjects;
using Ticket.DTOs.Common;
using Ticket.DTOs.Responses;
using Ticket.DTOs.ViewModels;

namespace Ticket.Mapping;

public class TicketProfile : Profile
{
    public TicketProfile()
    {
        CreateMap<Ticket.Domain.Entities.Ticket, TicketSummaryDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty))
            .ForMember(dest => dest.DepartmentId, opt => opt.MapFrom(src => src.DepartmentId))
            .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : string.Empty));

        CreateMap<Ticket.Domain.Entities.Ticket, TicketDetailsDto>()
            .IncludeBase<Ticket.Domain.Entities.Ticket, TicketSummaryDto>()
            .ForMember(dest => dest.Requester, opt => opt.MapFrom(src => src.Requester))
            .ForMember(dest => dest.Recipient, opt => opt.MapFrom(src => src.Recipient))
            .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata))
            .ForMember(dest => dest.History, opt => opt.MapFrom(src => src.History
                .OrderByDescending(h => h.OccurredAtUtc)
                .Select(h => h)))
            .ForMember(dest => dest.Department, opt => opt.MapFrom(src => src.Department))
            .ForMember(dest => dest.Comments, opt => opt.MapFrom(src => src.Comments
                .OrderByDescending(c => c.CreatedAtUtc)));

        CreateMap<TicketHistory, TicketHistoryDto>();
        CreateMap<Category, CategoryDto>();
        CreateMap<Ticket.Domain.Entities.Ticket, TimelineItemViewModel>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty));

        CreateMap<TicketContactInfoDto, TicketContactInfo>();
        CreateMap<TicketContactInfo, TicketContactInfoDto>();

        CreateMap<TicketMetadataDto, TicketMetadata>();
        CreateMap<TicketMetadata, TicketMetadataDto>();
        CreateMap<Department, DepartmentDto>();
        CreateMap<DepartmentMember, DepartmentMemberDto>();
        CreateMap<Department, TicketDepartmentDto>();
        CreateMap<TicketComment, TicketCommentDto>();
    }
}

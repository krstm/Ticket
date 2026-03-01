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
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty));

        CreateMap<Ticket.Domain.Entities.Ticket, TicketDetailsDto>()
            .IncludeBase<Ticket.Domain.Entities.Ticket, TicketSummaryDto>()
            .ForMember(dest => dest.Requester, opt => opt.MapFrom(src => src.Requester))
            .ForMember(dest => dest.Recipient, opt => opt.MapFrom(src => src.Recipient))
            .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata))
            .ForMember(dest => dest.History, opt => opt.MapFrom(src => src.History
                .OrderByDescending(h => h.OccurredAtUtc)
                .Select(h => h)));

        CreateMap<TicketHistory, TicketHistoryDto>();
        CreateMap<Category, CategoryDto>();
        CreateMap<Ticket.Domain.Entities.Ticket, TimelineItemViewModel>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty));

        CreateMap<TicketContactInfoDto, TicketContactInfo>();
        CreateMap<TicketContactInfo, TicketContactInfoDto>();

        CreateMap<TicketMetadataDto, TicketMetadata>();
        CreateMap<TicketMetadata, TicketMetadataDto>();
    }
}

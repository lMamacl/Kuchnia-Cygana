using AutoMapper;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Services;

namespace KuchniaUCygana.Application.Mappings;

/// <summary>
/// AutoMapper profile dla encji produkcyjnych i kompletacji.
/// </summary>
public sealed class ProductionProfile : Profile
{
    public ProductionProfile()
    {
        // ProductionPlanItem → ProductionPlanItemDto
        CreateMap<ProductionPlanItem, ProductionPlanItemDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.EstimatedReadyTime,
                o => o.MapFrom(s => s.EstimatedReadyTime.HasValue
                    ? s.EstimatedReadyTime.Value.ToString("HH:mm")
                    : null))
            .ForMember(d => d.ActualReadyTime,
                o => o.MapFrom(s => s.ActualReadyTime.HasValue
                    ? s.ActualReadyTime.Value.ToString("HH:mm")
                    : null));

        // ProductionPlan → ProductionPlanDto (Items mapowane ręcznie w serwisie)
        CreateMap<ProductionPlan, ProductionPlanDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Items, o => o.Ignore())
            .ForMember(d => d.FoodCostReport, o => o.Ignore());

        // PackingItem → PackingItemDto
        CreateMap<PackingItem, PackingItemDto>();

        // PackingSession → PackingSessionDto (Items mapowane ręcznie)
        CreateMap<PackingSession, PackingSessionDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Items, o => o.Ignore());

        // PackingLabel → PackingLabelDto
        CreateMap<PackingLabel, PackingLabelDto>()
            .ForMember(d => d.LabelType, o => o.MapFrom(s => s.LabelType.ToString()));

        // FoodCostReport → FoodCostReportDto (domain model → DTO)
        CreateMap<FoodCostReport, FoodCostReportDto>();
        CreateMap<FoodCostEntry, FoodCostEntryDto>();
    }
}

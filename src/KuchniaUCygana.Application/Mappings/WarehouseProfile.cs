using AutoMapper;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Services;

namespace KuchniaUCygana.Application.Mappings;

/// <summary>
/// AutoMapper profile dla encji magazynowych.
/// </summary>
public sealed class WarehouseProfile : Profile
{
    public WarehouseProfile()
    {
        // StockItem → StockItemDto (CurrentStock obliczany w serwisie)
        CreateMap<StockItem, StockItemDto>()
            .ForMember(d => d.CurrentStock, o => o.Ignore());

        // Batch → BatchDto
        CreateMap<Batch, BatchDto>();

        // TemperatureLog → TemperatureLogDto
        CreateMap<TemperatureLog, TemperatureLogDto>()
            .ForMember(d => d.IsOutOfRange,
                o => o.MapFrom(s =>
                    s.RecordedTemperatureCelsius < -25m || s.RecordedTemperatureCelsius > 8m));

        // InventoryAlert (domain) → InventoryAlertDto
        CreateMap<InventoryAlert, InventoryAlertDto>()
            .ForMember(d => d.AlertType, o => o.MapFrom(s => s.AlertType.ToString()));

        // BatchExpiryChangeLog → BatchExpiryChangeLogDto
        CreateMap<BatchExpiryChangeLog, BatchExpiryChangeLogDto>();
    }
}

using AutoMapper;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.Mappings;

public sealed class LogisticsProfile : Profile
{
    public LogisticsProfile()
    {
        // Vehicle mappings
        CreateMap<Vehicle, VehicleDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        CreateMap<CreateVehicleRequest, Vehicle>();
        CreateMap<UpdateVehicleRequest, Vehicle>();

        // DeliveryRoute mappings (z uwzględnieniem przystanków)
        CreateMap<DeliveryRoute, DeliveryRouteDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.DriverName, opt => opt.Ignore()) // będzie wypełniane ręcznie lub z osobnego repozytorium
            .ForMember(dest => dest.VehicleRegistration, opt => opt.Ignore())
            .ForMember(dest => dest.Stops, opt => opt.MapFrom(src => src.Stops ?? new List<DeliveryRouteStop>()));

        CreateMap<DeliveryRouteStop, RouteStopDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.FullAddress, opt => opt.Ignore()); // adres będzie uzupełniany osobno (z modułu 1)

        // Request mappings (dla tworzenia/edycji)
        CreateMap<CreateRouteRequest, DeliveryRoute>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => RouteStatus.Created))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTimeOffset.UtcNow));
    }
}
using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Application.Interfaces;

public interface IDeliveryRouteService
{
    Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesForDateAsync(DateTimeOffset date);

    Task<DeliveryRouteDto?> GetRouteDetailsAsync(int routeId);

    Task<DailyRouteGenerationResultDto> GenerateDailyRoutesAsync(GenerateDailyRoutesRequest request);
}

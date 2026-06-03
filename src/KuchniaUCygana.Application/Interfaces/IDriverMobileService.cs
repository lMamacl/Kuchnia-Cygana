using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Application.Interfaces;

public interface IDriverMobileService
{
    Task<DriverDashboardDto> GetDashboardAsync();

    Task<DriverRouteStopDto?> GetStopAsync(int stopId);

    Task StartRouteAsync();

    Task StartStopAsync(int stopId);

    Task ConfirmDeliveryAsync(int stopId, ConfirmDriverDeliveryRequest request);

    Task ReportProblemAsync(int stopId, ReportDriverDeliveryProblemRequest request);

    Task<DriverRouteSummaryDto> GetSummaryAsync();
}

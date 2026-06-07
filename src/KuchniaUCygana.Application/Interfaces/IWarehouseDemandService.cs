using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Interfaces;

public interface IWarehouseDemandService
{
    Task<WarehouseDemandDto> GetDemandAsync(DateOnly startDate, int days);
}

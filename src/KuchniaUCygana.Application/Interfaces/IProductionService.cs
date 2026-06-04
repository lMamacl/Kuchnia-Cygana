using KuchniaUCygana.Application.DTOs.Production;

namespace KuchniaUCygana.Application.Interfaces;

/// <summary>
/// Serwis aplikacyjny produkcji — plan, karta produkcyjna, zatwierdzanie, półprodukty.
/// </summary>
public interface IProductionService
{
    Task<ProductionPlanDto> GenerateDailyPlanAsync(CreateProductionPlanRequest request);

    Task<ProductionPlanDto?> GetDailyPlanByDateAsync(DateOnly date);

    Task<ProductionPlanDto?> GetPlanByIdAsync(int planId);

    Task<KitchenDashboardDto> GetKitchenDashboardAsync(KitchenDashboardFilterDto filter);

    Task<CookingCardDto> GetCookingCardAsync(int planItemId);

    Task ApproveCookingAsync(int planItemId, decimal actualQuantity);

    Task ProduceSemiFinishedAsync(int planId);
}

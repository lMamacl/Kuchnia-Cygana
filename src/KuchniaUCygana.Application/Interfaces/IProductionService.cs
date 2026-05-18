using KuchniaUCygana.Application.DTOs.Production;

namespace KuchniaUCygana.Application.Interfaces;

/// <summary>
/// Serwis aplikacyjny produkcji — plan, karta produkcyjna, zatwierdzanie, półprodukty.
/// </summary>
public interface IProductionService
{
    Task<ProductionPlanDto> GenerateDailyPlanAsync(CreateProductionPlanRequest request);

    Task<CookingCardDto> GetCookingCardAsync(int planItemId);

    Task ApproveCookingAsync(int planItemId, decimal actualQuantity);

    Task ProduceSemiFinishedAsync(int planId);
}

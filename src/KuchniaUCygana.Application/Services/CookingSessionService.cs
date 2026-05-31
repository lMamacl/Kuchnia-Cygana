using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class CookingSessionService : ICookingSessionService
{
    private readonly IRepository<ProductionPlanItem> _itemRepository;

    public CookingSessionService(IRepository<ProductionPlanItem> itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task StartComponentSessionAsync(int productionPlanItemId)
    {
        var item = await _itemRepository.GetByIdAsync(productionPlanItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {productionPlanItemId} nie istnieje.");

        if (!item.FefoDeductedAt.HasValue)
        {
            throw new InvalidOperationException("Sesje gotowania mozna uruchomic dopiero po idempotentnym zdjeciu FEFO dla pozycji.");
        }

        if (item.Status == ProductionItemStatus.Planned)
        {
            item.Status = ProductionItemStatus.Cooking;
            await _itemRepository.UpdateAsync(item);
        }
    }

    public async Task ApproveComponentCookingAsync(int productionPlanItemId, decimal acceptedQuantity, string approvedBy)
    {
        var item = await _itemRepository.GetByIdAsync(productionPlanItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {productionPlanItemId} nie istnieje.");

        item.CookedQuantity = (int)acceptedQuantity;
        item.Status = ProductionItemStatus.Cooked;
        item.ActualReadyTime = TimeOnly.FromDateTime(DateTime.Now);
        await _itemRepository.UpdateAsync(item);
    }
}

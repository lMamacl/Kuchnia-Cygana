using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Production;

namespace KuchniaUCygana.Domain.Interfaces.Production;

public interface IProductionPlanRepository : IRepository<ProductionPlan>
{
    /// <summary>
    /// Pobiera plan produkcji na konkretną datę.
    /// </summary>
    Task<ProductionPlan?> GetByDateAsync(DateOnly date);

    /// <summary>
    /// Pobiera plan z załadowanymi pozycjami (eager loading).
    /// </summary>
    Task<ProductionPlan?> GetWithItemsAsync(int planId);

    /// <summary>
    /// Pobiera pozycje planu.
    /// </summary>
    Task<IEnumerable<ProductionPlanItem>> GetPlanItemsAsync(int planId);
}

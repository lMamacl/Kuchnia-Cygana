using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IDietVariantMealRepository
{
    Task<DietVariantMeal?> GetAsync(int variantId, int mealId);

    Task<IEnumerable<DietVariantMeal>> GetByVariantIdAsync(int variantId);

    Task UpsertAsync(DietVariantMeal assignment);
}

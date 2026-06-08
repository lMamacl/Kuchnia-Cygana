using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IIngredientRepository : IRepository<Ingredient>
{
    Task<IngredientSearchResult> SearchAsync(IngredientSearchQuery query);

    Task<IReadOnlyList<IngredientListRow>> SearchRecipeLookupAsync(string? query, int page, int pageSize);

    Task<IReadOnlyList<IngredientAllergenRow>> GetIngredientAllergensAsync(int ingredientId);

    Task SaveIngredientAllergensAsync(int ingredientId, IReadOnlyList<IngredientAllergen> allergens);

    Task<bool> CanDeleteAsync(int ingredientId);
}

public sealed class IngredientSearchQuery
{
    public string? Search { get; set; }

    public string? ResourceType { get; set; }

    public int? FoodCategoryId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public int? AllergenId { get; set; }

    public bool MissingWarehouseMapping { get; set; }

    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}

public sealed class IngredientSearchResult
{
    public IReadOnlyList<IngredientListRow> Items { get; set; } = Array.Empty<IngredientListRow>();

    public int TotalCount { get; set; }
}

public sealed class IngredientListRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ResourceType { get; set; } = "Food";

    public int? FoodCategoryId { get; set; }

    public string? FoodCategoryName { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal CostPerUnit { get; set; }

    public string? Notes { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public bool MissingWarehouseMapping { get; set; }

    public bool IsActive { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    public string? AllergenNames { get; set; }
}

public sealed class IngredientAllergenRow
{
    public int IngredientId { get; set; }

    public int AllergenId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? IconUrl { get; set; }

    public bool TraceAmount { get; set; }
}

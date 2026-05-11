using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Adapters;

/// <summary>
/// Adapter IDietDataProvider oparty na prawdziwych encjach Modułu 2 (Menu).
/// Odpytuje bezpośrednio tabele: DietVariantMeals, Meals, Recipes, Ingredients.
/// </summary>
public sealed class DietDataAdapter : IDietDataProvider
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DietDataAdapter(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Pobiera plan diet na 7 dni od podanej daty.
    /// Zwraca listę posiłków przypisanych do wariantów kalorycznych.
    /// Uwaga: w obecnym modelu M2 DietVariantMeal nie ma daty — 
    /// zwracamy wszystkie aktywne przypisania (posiłki powtarzają się cyklicznie).
    /// </summary>
    public async Task<IEnumerable<DietPlanEntry>> Get7DayPlanAsync(DateOnly startDate)
    {
        using var db = _connectionFactory.CreateConnection();

        // JOIN: DietVariantMeal → Meal (tylko aktywne posiłki)
        var query = db.From<DietVariantMeal>()
            .Join<DietVariantMeal, Meal>((dvm, m) => dvm.MealId == m.Id)
            .Where<Meal>(m => m.IsActive);

        var dvMeals = await db.SelectAsync<DietVariantMealWithName>(query);

        return dvMeals.Select(dvm => new DietPlanEntry
        {
            MealId = dvm.MealId,
            MealName = dvm.MealName,
            DietVariantId = dvm.DietVariantId,
            ServingWeightGrams = dvm.ServingSizeMultiplier * 100m, // Base 100g × multiplier
        });
    }

    /// <summary>
    /// Pobiera recepturę posiłku — listę składników z gramaturami.
    /// Dane z tabel: Recipe + Ingredient.
    /// </summary>
    public async Task<IEnumerable<RecipeIngredientEntry>> GetRecipeForMealAsync(int mealId)
    {
        using var db = _connectionFactory.CreateConnection();

        var query = db.From<Recipe>()
            .Join<Recipe, Ingredient>((r, ing) => r.IngredientId == ing.Id)
            .Where<Recipe>(r => r.MealId == mealId);

        var recipeRows = await db.SelectAsync<RecipeIngredientRow>(query);

        return recipeRows.Select(r => new RecipeIngredientEntry
        {
            IngredientId = r.IngredientId,
            IngredientName = r.IngredientName,
            WeightInGrams = r.WeightInGrams,
            IsOptional = r.IsOptional,
        });
    }

    // Wewnętrzne DTO do OrmLite JOIN projection
    private sealed class DietVariantMealWithName
    {
        public int MealId { get; set; }

        public string MealName { get; set; } = string.Empty;

        public int DietVariantId { get; set; }

        public decimal ServingSizeMultiplier { get; set; }
    }

    private sealed class RecipeIngredientRow
    {
        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public decimal WeightInGrams { get; set; }

        public bool IsOptional { get; set; }
    }
}

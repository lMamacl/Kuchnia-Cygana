using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Application.Services;

public sealed class DietOrderingService : IDietOrderingService
{
    private const decimal DefaultBasePricePerDay = 59.99m;

    private readonly IDietCatalogProvider dietCatalogProvider;

    public DietOrderingService(IDietCatalogProvider dietCatalogProvider)
    {
        this.dietCatalogProvider = dietCatalogProvider;
    }

    public decimal BasePricePerDay => DefaultBasePricePerDay;

    public decimal CalculatePricePerDay(decimal priceMultiplier)
        => Math.Round(BasePricePerDay * priceMultiplier, 2, MidpointRounding.AwayFromZero);

    public decimal CalculatePricePerDay(DietCatalogVariantDto variant)
        => CalculatePricePerDay(variant.PriceMultiplier);

    public async Task<CartItemDto> CreateCartItemAsync(int dietVariantId, int totalDays)
    {
        if (dietVariantId <= 0)
            throw new ArgumentOutOfRangeException(nameof(dietVariantId), "Wybierz wariant diety.");

        if (totalDays is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(totalDays), "Liczba dni musi byc z zakresu 1-365.");

        var variant = await dietCatalogProvider.GetVariantAsync(dietVariantId)
            ?? throw new InvalidOperationException("Wybrany wariant diety nie istnieje.");

        if (!variant.IsAvailable)
            throw new InvalidOperationException("Wybrany wariant diety nie jest aktualnie dostepny.");

        var diet = await dietCatalogProvider.GetDietAsync(variant.DietId)
            ?? throw new InvalidOperationException("Dieta przypisana do wariantu nie istnieje.");

        if (!diet.IsActive || !IsPublishedOrActive(diet.Status))
            throw new InvalidOperationException("Wybrana dieta nie jest aktualnie dostepna.");

        return new CartItemDto
        {
            DietId = diet.DietId,
            DietVariantId = variant.DietVariantId,
            DietName = diet.Name,
            VariantName = variant.Name,
            CaloriesPerDay = variant.TargetCalories,
            PricePerDay = CalculatePricePerDay(variant),
            TotalDays = totalDays,
        };
    }

    private static bool IsPublishedOrActive(string status)
        => string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
}

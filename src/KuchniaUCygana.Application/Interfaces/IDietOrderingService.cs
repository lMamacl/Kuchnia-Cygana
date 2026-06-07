using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Application.Interfaces;

public interface IDietOrderingService
{
    decimal BasePricePerDay { get; }

    decimal CalculatePricePerDay(decimal priceMultiplier);

    decimal CalculatePricePerDay(DietCatalogVariantDto variant);

    Task<CartItemDto> CreateCartItemAsync(int dietVariantId, int totalDays);
}

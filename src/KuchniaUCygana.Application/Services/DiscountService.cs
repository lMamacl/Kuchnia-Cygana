using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class DiscountService : IDiscountService
{
    private readonly IDiscountCodeRepository discountCodeRepository;
    private readonly IOrderRepository orderRepository;

    public DiscountService(
        IDiscountCodeRepository discountCodeRepository,
        IOrderRepository orderRepository)
    {
        this.discountCodeRepository = discountCodeRepository;
        this.orderRepository = orderRepository;
    }

    public async Task<DiscountValidationResult> ValidateAndApplyAsync(
        string code, int orderId, int customerId)
    {
        var discount = await discountCodeRepository.GetByCodeAsync(code);

        if (discount is null || !discount.IsActive)
            return Fail("Kod rabatowy nie istnieje lub jest nieaktywny.");

        var now = DateTimeOffset.UtcNow;

        if (discount.ValidFrom.HasValue && discount.ValidFrom > now)
            return Fail("Kod rabatowy nie jest jeszcze aktywny.");

        if (discount.ValidTo.HasValue && discount.ValidTo < now)
            return Fail("Kod rabatowy wygasl.");

        if (discount.MaxUsageCount.HasValue && discount.UsedCount >= discount.MaxUsageCount)
            return Fail("Kod rabatowy zostal juz wykorzystany maksymalna liczbe razy.");

        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null || order.CustomerId != customerId)
            return Fail("Zamowienie nie istnieje.");

        if (discount.MinimumOrderValue.HasValue && order.TotalPrice < discount.MinimumOrderValue)
            return Fail($"Minimalna wartosc zamowienia dla tego kodu to {discount.MinimumOrderValue.Value:C}.");

        // Oblicz kwote rabatu
        var discountAmount = discount.DiscountType switch
        {
            DiscountType.Percentage =>
                Math.Round(order.TotalPrice * discount.DiscountValue / 100m, 2),
            DiscountType.Fixed =>
                Math.Min(discount.DiscountValue, order.TotalPrice),
            _ => 0m,
        };

        // Zapisz rabat w zamowieniu
        order.DiscountCodeId = discount.Id;
        order.DiscountAmount = discountAmount;
        order.FinalPrice = order.TotalPrice - discountAmount;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await orderRepository.UpdateAsync(order);

        // Zinkrementuj licznik uzyc
        await discountCodeRepository.IncrementUsageAsync(discount.Id);

        return new DiscountValidationResult
        {
            IsValid = true,
            DiscountAmount = discountAmount,
            NewFinalPrice = order.FinalPrice,
        };
    }

    public async Task<bool> RemoveDiscountAsync(int orderId, int customerId)
    {
        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null || order.CustomerId != customerId) return false;

        order.DiscountCodeId = null;
        order.DiscountAmount = 0m;
        order.FinalPrice = order.TotalPrice;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        return await orderRepository.UpdateAsync(order);
    }

    private static DiscountValidationResult Fail(string message) =>
        new() { IsValid = false, ErrorMessage = message };
}

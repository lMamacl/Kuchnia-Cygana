using System.Text.Json;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class CartService : ICartService
{
    private const string EmptyCart = "{}";

    public CartDto GetCart(string? serializedCart)
    {
        if (string.IsNullOrWhiteSpace(serializedCart))
            return new CartDto();

        try
        {
            return JsonSerializer.Deserialize<CartDto>(serializedCart) ?? new CartDto();
        }
        catch (JsonException)
        {
            return new CartDto();
        }
    }

    public CartDto AddItem(CartDto cart, CartItemDto item)
    {
        var existing = cart.Items.FirstOrDefault(i => i.DietVariantId == item.DietVariantId);
        if (existing is not null)
            existing.TotalDays += item.TotalDays;
        else
            cart.Items.Add(item);

        return cart;
    }

    public CartDto RemoveItem(CartDto cart, int dietVariantId)
    {
        cart.Items.RemoveAll(i => i.DietVariantId == dietVariantId);
        return cart;
    }

    public CartDto ClearCart() => new CartDto();

    public string Serialize(CartDto cart) => JsonSerializer.Serialize(cart);
}

using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Interfaces;

public interface ICartService
{
    CartDto GetCart(string? serializedCart);
    CartDto AddItem(CartDto cart, CartItemDto item);
    CartDto RemoveItem(CartDto cart, int dietVariantId);
    CartDto ClearCart();
    string Serialize(CartDto cart);
}

namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class CheckoutSummaryDto
{
    public int OrderId { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public List<DeliveryCalendarDto> DeliveryDays { get; set; } = new();
    public AddressDto? SelectedAddress { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
    public string? AppliedDiscountCode { get; set; }
}

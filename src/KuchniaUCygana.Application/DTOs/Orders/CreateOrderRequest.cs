namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class CreateOrderRequest
{
    public int AddressId { get; set; }
    public int? DeliveryWindowId { get; set; }
    public DateTime StartDate { get; set; }
    public string? Notes { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public sealed class CreateOrderItemRequest
{
    public int DietId { get; set; }
    public int DietVariantId { get; set; }
    public string DietName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public int CaloriesPerDay { get; set; }
    public decimal PricePerDay { get; set; }
    public int TotalDays { get; set; }
}

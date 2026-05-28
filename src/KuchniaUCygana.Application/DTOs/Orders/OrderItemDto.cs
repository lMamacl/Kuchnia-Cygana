namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class OrderItemDto
{
    public int Id { get; set; }
    public string DietName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public int CaloriesPerDay { get; set; }
    public decimal PricePerDay { get; set; }
    public int TotalDays { get; set; }
    public decimal TotalPrice { get; set; }
}

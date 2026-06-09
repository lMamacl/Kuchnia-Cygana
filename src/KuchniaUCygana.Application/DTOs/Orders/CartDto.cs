namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class CartDto
{
    public List<CartItemDto> Items { get; set; } = new();
    public decimal TotalPrice => Items.Sum(i => i.TotalPrice);
    public int TotalDays => Items.Sum(i => i.TotalDays);
}

public sealed class CartItemDto
{
    public int DietId { get; set; }
    public int DietVariantId { get; set; }
    public string DietName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public int CaloriesPerDay { get; set; }
    public decimal PricePerDay { get; set; }
    public int TotalDays { get; set; }
    public decimal TotalPrice => PricePerDay * TotalDays;
}

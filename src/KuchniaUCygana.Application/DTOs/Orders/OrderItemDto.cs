namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class OrderItemDto
{
    public int Id { get; set; }
    public int DietId { get; set; }
    public int DietVariantId { get; set; }
    public int? MealId { get; set; }
    public int? MealVariantId { get; set; }
    public int? DietMenuPlanItemId { get; set; }
    public string DietName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string? MealSlot { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public int CaloriesPerDay { get; set; }
    public decimal PricePerDay { get; set; }
    public int TotalDays { get; set; }
    public decimal TotalPrice { get; set; }
}

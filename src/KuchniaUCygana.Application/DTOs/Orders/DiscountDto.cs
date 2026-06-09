namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class ApplyDiscountRequest
{
    public int OrderId { get; set; }
    public string Code { get; set; } = string.Empty;
}

public sealed class DiscountValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NewFinalPrice { get; set; }
}

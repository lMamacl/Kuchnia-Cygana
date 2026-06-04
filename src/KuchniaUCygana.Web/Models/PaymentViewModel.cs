namespace KuchniaUCygana.Web.Models;

public sealed class PaymentViewModel
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal FinalPrice { get; set; }
    public string StripeClientSecret { get; set; } = string.Empty;
    public string StripePublishableKey { get; set; } = string.Empty;
}

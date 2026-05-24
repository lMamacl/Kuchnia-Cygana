using System.ComponentModel.DataAnnotations;
using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Web.Models;

public sealed class CheckoutIndexViewModel
{
    public IEnumerable<AddressDto> Addresses { get; set; } = [];
    public IEnumerable<DeliveryWindowDto> Windows { get; set; } = [];
    public CartDto Cart { get; set; } = new();

    [Required(ErrorMessage = "Wybierz adres dostawy.")]
    [Range(1, int.MaxValue, ErrorMessage = "Wybierz adres dostawy.")]
    public int SelectedAddressId { get; set; }

    public int? SelectedWindowId { get; set; }

    [Required(ErrorMessage = "Podaj date rozpoczecia.")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today.AddDays(1);

    public string? Notes { get; set; }
}

public sealed class DiscountPartialViewModel
{
    public int OrderId { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
    public string? ErrorMessage { get; set; }
    public bool WasApplied { get; set; }
    public string? AppliedCode { get; set; }
}

public sealed class OrderChangeDeliveryViewModel
{
    public int DeliveryCalendarId { get; set; }
    public DateTime CurrentDate { get; set; }
    public DateTime NewDate { get; set; }
    public int? NewAddressId { get; set; }
    public IEnumerable<AddressDto> Addresses { get; set; } = [];
}

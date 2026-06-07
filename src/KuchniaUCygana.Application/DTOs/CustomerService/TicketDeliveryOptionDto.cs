namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class TicketDeliveryOptionDto
{
    public int DeliveryCalendarId { get; set; }

    public int OrderId { get; set; }

    public int CustomerId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string CustomerFullName { get; set; } = string.Empty;

    public DateTime DeliveryDate { get; set; }

    public string DeliveryStatus { get; set; } = string.Empty;

    public string AddressFullLine { get; set; } = string.Empty;

    public string DietSummary { get; set; } = string.Empty;

    public string Label => string.IsNullOrWhiteSpace(DietSummary)
        ? $"{DeliveryDate:dd.MM.yyyy} | {OrderNumber} | {CustomerFullName} | {AddressFullLine}"
        : $"{DeliveryDate:dd.MM.yyyy} | {OrderNumber} | {CustomerFullName} | {DietSummary}";
}

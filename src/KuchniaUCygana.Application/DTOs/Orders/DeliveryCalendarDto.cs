using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class DeliveryCalendarDto
{
    public int Id { get; set; }
    public DateTime DeliveryDate { get; set; }
    public DeliveryStatus Status { get; set; }
    public bool IsSkipped { get; set; }
    public string? DeliveryWindowName { get; set; }
    public int AddressId { get; set; }
    public string AddressFullLine { get; set; } = string.Empty;
}

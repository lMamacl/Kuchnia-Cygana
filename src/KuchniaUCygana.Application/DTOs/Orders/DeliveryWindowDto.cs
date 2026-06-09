namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class DeliveryWindowDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}

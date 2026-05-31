namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class RegisterIncidentWasteRequest
{
    public int IncidentId { get; set; }

    public string? Notes { get; set; }
}

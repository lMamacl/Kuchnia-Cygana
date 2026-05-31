namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class HandlePackingIncidentRequest
{
    public int IncidentId { get; set; }

    public string? Notes { get; set; }
}

namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class ResolvePackingIncidentRequest
{
    public int IncidentId { get; set; }

    public string? ResolutionNotes { get; set; }
}

using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class CreatePackingBagIssueRequest
{
    public int PackingBagId { get; set; }

    public IReadOnlyCollection<PackingIncidentReasonFlag> ReasonFlags { get; set; } = Array.Empty<PackingIncidentReasonFlag>();

    public string? Description { get; set; }

    public bool AllowVerifiedManifestChange { get; set; }
}

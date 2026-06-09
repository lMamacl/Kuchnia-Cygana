using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class CreatePackingItemIssueRequest
{
    public int PackingItemId { get; set; }

    public IReadOnlyCollection<PackingIncidentReasonFlag> ReasonFlags { get; set; } = Array.Empty<PackingIncidentReasonFlag>();

    public string? Description { get; set; }
}

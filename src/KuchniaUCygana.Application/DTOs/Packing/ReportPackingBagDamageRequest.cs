namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class ReportPackingBagDamageRequest
{
    public int PackingBagId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public bool AllowVerifiedManifestChange { get; set; }
}

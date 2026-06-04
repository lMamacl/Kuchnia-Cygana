namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class ReportPackingItemIssueRequest
{
    public int PackingItemId { get; set; }

    public string IssueType { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

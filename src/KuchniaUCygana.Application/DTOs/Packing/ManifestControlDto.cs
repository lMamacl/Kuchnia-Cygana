namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class ManifestControlDto
{
    public DateOnly Date { get; set; }

    public PackingRouteDto Route { get; set; } = new();

    public PackingManifestDto? Manifest { get; set; }

    public List<ManifestChecklistItemDto> Checklist { get; set; } = new();

    public List<PackingManifestIssueDto> Issues { get; set; } = new();

    public bool HasManifest => Manifest is not null;

    public int BlockingIssueCount => Issues.Count(issue => issue.IsBlocking && !issue.IsResolved);

    public bool HasBlockingIssues => BlockingIssueCount > 0;

    public bool CanGenerateManifest { get; set; }

    public bool CanWorkerApprove { get; set; }

    public bool CanSupervisorApprove { get; set; }
}

public sealed class ManifestChecklistItemDto
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public bool IsBlocking { get; set; } = true;
}

public sealed class PackingManifestIssueDto
{
    public string IssueType { get; set; } = string.Empty;

    public string Status { get; set; } = "Nowe";

    public bool IsBlocking { get; set; } = true;

    public bool IsResolved { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public string? SourceType { get; set; }

    public int? SourceId { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Packing;

[Table("PackingManifestIssues")]
public sealed class PackingManifestIssue : BaseEntity<int>
{
    public int? PackingManifestId { get; set; }

    public DateOnly PackingDate { get; set; }

    public int RouteId { get; set; }

    [StringLength(80)]
    public string IssueType { get; set; } = string.Empty;

    public PackingManifestIssueStatus Status { get; set; } = PackingManifestIssueStatus.New;

    public bool IsBlocking { get; set; } = true;

    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Details { get; set; } = string.Empty;

    [StringLength(50)]
    public string? SourceType { get; set; }

    public int? SourceId { get; set; }

    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ResolvedAt { get; set; }

    public int? ResolvedByUserId { get; set; }

    [StringLength(1000)]
    public string? ResolutionNotes { get; set; }
}

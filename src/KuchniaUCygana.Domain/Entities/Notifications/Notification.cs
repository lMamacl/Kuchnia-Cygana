using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Notifications;

[Table("Notifications")]
public sealed class Notification : BaseEntity<long>
{
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Severity { get; set; } = NotificationSeverity.Info;

    [Required]
    [StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [StringLength(300)]
    public string? LinkUrl { get; set; }

    [StringLength(160)]
    public string? DeduplicationKey { get; set; }

    [StringLength(50)]
    public string? SourceType { get; set; }

    public long? SourceId { get; set; }
}

public static class NotificationSeverity
{
    public const string Info = "Info";
    public const string Warning = "Warning";
    public const string Danger = "Danger";
    public const string Success = "Success";
}

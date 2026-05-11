using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class Diet : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public DietStatus Status { get; set; } = DietStatus.Draft;

    public bool IsActive { get; set; } = true;

    public string? ThumbnailUrl { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Production;

/// <summary>
/// Etykieta produktowa pojedynczego pudelka/dania.
/// Kazdy druk i redruk tworzy osobny snapshot.
/// </summary>
[Table("BoxLabels")]
public sealed class BoxLabel : BaseEntity<int>
{
    public int PackingItemId { get; set; }

    [Required]
    [StringLength(100)]
    public string QrCode { get; set; } = string.Empty;

    [Required]
    public string LabelDataJson { get; set; } = string.Empty;

    public int PrintNumber { get; set; } = 1;

    [StringLength(250)]
    public string? ReprintReason { get; set; }

    public DateTimeOffset PrintedAt { get; set; } = DateTimeOffset.UtcNow;

    [StringLength(100)]
    public string? PrintedBy { get; set; }
}

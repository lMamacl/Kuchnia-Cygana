using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

/// <summary>
/// Log zmian daty ważności partii — tworzy ślad audytowy każdej modyfikacji.
/// Odpowiada tabeli BatchExpiryChangeLogs (migracja 011).
/// </summary>
[Table("BatchExpiryChangeLogs")]
public class BatchExpiryChangeLog : BaseEntity<int>
{
    /// <summary>
    /// ID partii której dotyczy zmiana.
    /// </summary>
    public int BatchId { get; set; }

    /// <summary>
    /// Poprzednia data ważności.
    /// </summary>
    public DateTime OldExpiryDate { get; set; }

    /// <summary>
    /// Nowa data ważności po zmianie.
    /// </summary>
    public DateTime NewExpiryDate { get; set; }

    /// <summary>
    /// Uzasadnienie zmiany (wymagane — min. 5 znaków, walidator: EditBatchExpiryValidator).
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// ID użytkownika który dokonał zmiany (z ICurrentUserService).
    /// </summary>
    public int? ChangedByUserId { get; set; }

    /// <summary>
    /// Data i czas zmiany (UTC).
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

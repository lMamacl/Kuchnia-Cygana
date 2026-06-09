using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Packing;

/// <summary>
/// Log zmian statusu sesji pakowania — pełny ślad audytowy przejść statusów torby.
/// Odpowiada tabeli PackingStatusLogs (migracja 012).
/// </summary>
[Table("PackingStatusLogs")]
public class PackingStatusLog : BaseEntity<int>
{
    /// <summary>
    /// ID sesji pakowania której dotyczy zmiana statusu.
    /// </summary>
    public int PackingSessionId { get; set; }

    /// <summary>
    /// Poprzedni status sesji.
    /// </summary>
    public PackingStatus OldStatus { get; set; }

    /// <summary>
    /// Nowy status sesji po zmianie.
    /// </summary>
    public PackingStatus NewStatus { get; set; }

    /// <summary>
    /// ID użytkownika który dokonał zmiany (z ICurrentUserService).
    /// </summary>
    public int? ChangedByUserId { get; set; }

    /// <summary>
    /// Data i czas zmiany (UTC).
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Opcjonalne notatki do zmiany statusu (np. powód cofnięcia).
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}

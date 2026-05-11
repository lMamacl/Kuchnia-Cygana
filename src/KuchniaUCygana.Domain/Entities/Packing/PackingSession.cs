using System;
using ServiceStack.DataAnnotations;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Packing;

/// <summary>
/// Sesja pakowania — odpowiada koncepcji Torba z class diagram.puml.
/// Grupuje pudełka dla jednego zamówienia/klienta.
/// </summary>
[Alias("PackingSessions")]
public class PackingSession : AuditableEntity<int>
{
    /// <summary>
    /// Data sesji pakowania.
    /// </summary>
    public DateOnly PackingDate { get; set; }

    /// <summary>
    /// ID zamówienia z Modułu 1 (bridge — bez strict FK).
    /// </summary>
    public int? OrderId { get; set; }

    /// <summary>
    /// Nazwa klienta (denormalizowana z M1 dla etykiet).
    /// </summary>
    [StringLength(150)]
    public string? ClientName { get; set; }

    /// <summary>
    /// Kto pakował (username magazyniera).
    /// </summary>
    [StringLength(50)]
    public string? PackedBy { get; set; }

    /// <summary>
    /// Status kompletacji.
    /// </summary>
    public PackingStatus Status { get; set; } = PackingStatus.Pending;

    /// <summary>
    /// ID trasy z Modułu 4 (bridge do logistyki).
    /// </summary>
    public int? RouteId { get; set; }

    /// <summary>
    /// Numer stopu na trasie.
    /// </summary>
    public int? StopNumber { get; set; }

    /// <summary>
    /// Lista pozycji w sesji pakowania.
    /// </summary>
    [Ignore]
    public System.Collections.Generic.List<PackingItem> Items { get; set; } = new();
}

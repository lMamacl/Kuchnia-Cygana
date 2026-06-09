using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Packing;

/// <summary>
/// Sesja pakowania — odpowiada koncepcji Torba z class diagram.puml.
/// Grupuje pudełka dla jednego zamówienia/klienta.
/// </summary>
[Table("PackingSessions")]
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

    [StringLength(50)]
    public string? ClientPublicId { get; set; }

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
    /// ID z tabeli DeliveryCalendar (M1) — klucz integracyjny z M4.
    /// Pozwala na bezpośredni JOIN z DeliveryRouteStops (M4) w celu
    /// dynamicznego pobrania RouteId i StopNumber.
    /// Ustalenia architektoniczne M3↔M4 z 28.05.2026.
    /// </summary>
    public int? DeliveryCalendarId { get; set; }

    /// <summary>
    /// Lista pozycji w sesji pakowania.
    /// </summary>
    [NotMapped]
    public System.Collections.Generic.List<PackingItem> Items { get; set; } = new();
}

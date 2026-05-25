using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status cyklu kompletacji i zaladunku paczki/torby.
/// </summary>
public enum PackingStatus
{
    Pending = 0,
    Packed = 1,
    // Etykieta transportowa torby zostala przygotowana. Folia dan jest osobnym etapem kuchni.
    Labeled = 2,
    Loaded = 3,
    Dispatched = 4,
    Delivered = 5,
    DeliveryFailed = 6,
}

namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status pojedynczego pudelka w torbie zamowienia.
/// </summary>
public enum PackingItemStatus
{
    Pending = 0,
    FoilPrinted = 1,
    Packed = 2,
    Damaged = 3,
    Missing = 4,
}

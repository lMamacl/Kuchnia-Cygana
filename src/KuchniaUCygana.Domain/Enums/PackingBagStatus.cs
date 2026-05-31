namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status fizycznej torby transportowej w ramach jednej logicznej dostawy.
/// </summary>
public enum PackingBagStatus
{
    Pending = 0,
    Packed = 1,
    Labeled = 2,
    Manifested = 3,
    Loaded = 4,
    Dispatched = 5,
    Damaged = 6,
}

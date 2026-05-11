using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status cyklu kompletacji i zaladunku paczki/torby.
/// </summary>
[EnumAsInt]
public enum PackingStatus
{
    Pending = 0,
    Packed = 1,
    Labeled = 2,
    Loaded = 3,
    Dispatched = 4,
}

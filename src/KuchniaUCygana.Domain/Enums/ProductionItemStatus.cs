using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status realizacji pojedynczej pozycji planu produkcji.
/// </summary>
[EnumAsInt]
public enum ProductionItemStatus
{
    Planned = 0,
    Cooking = 1,
    Cooked = 2,
    Failed = 3,
}

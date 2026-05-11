using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status cyklu zycia planu produkcji.
/// </summary>
[EnumAsInt]
public enum ProductionPlanStatus
{
    Draft = 0,
    Active = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}

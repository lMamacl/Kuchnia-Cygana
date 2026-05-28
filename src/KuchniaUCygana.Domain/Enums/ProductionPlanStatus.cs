using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status cyklu zycia planu produkcji.
/// </summary>
public enum ProductionPlanStatus
{
    Draft = 0,
    Active = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status realizacji pojedynczej pozycji planu produkcji.
/// </summary>
public enum ProductionItemStatus
{
    Planned = 0,
    Cooking = 1,
    Cooked = 2,
    Failed = 3,
}

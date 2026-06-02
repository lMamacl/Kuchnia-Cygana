using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("DietMenuPlanItems")]
public sealed class DietMenuPlanItem : AuditableEntity
{
    public int DietMenuPlanId { get; set; }

    public int DietVariantId { get; set; }

    public int MealId { get; set; }

    public int? MealVariantId { get; set; }

    [StringLength(40)]
    public string MealSlot { get; set; } = string.Empty;

    public decimal ServingSizeMultiplier { get; set; } = 1.0m;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

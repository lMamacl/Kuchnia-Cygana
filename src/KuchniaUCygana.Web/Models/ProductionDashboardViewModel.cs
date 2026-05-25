using System;
using KuchniaUCygana.Application.DTOs.Production;

namespace KuchniaUCygana.Web.Models;

/// <summary>
/// Model widoku dla pulpitu produkcji.
/// </summary>
public sealed class ProductionDashboardViewModel
{
    /// <summary>
    /// Wybrany dzień produkcji.
    /// </summary>
    public DateOnly SelectedDate { get; set; }

    /// <summary>
    /// Dane planu produkcji na wybrany dzień (lub null, jeśli nie wygenerowano).
    /// </summary>
    public ProductionPlanDto? DailyPlan { get; set; }
}

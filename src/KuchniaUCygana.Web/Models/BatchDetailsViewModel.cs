using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Web.Models;

/// <summary>
/// Model widoku dla szczegółów składnika i jego partii.
/// </summary>
public sealed class BatchDetailsViewModel
{
    public StockItemDetailsDto Details { get; set; } = null!;
}

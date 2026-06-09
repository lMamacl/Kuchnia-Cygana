using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Web.Models;

public sealed class PackingSessionViewModel
{
    public DateOnly SelectedDate { get; set; }

    public PackingSessionDto Session { get; set; } = null!;

    public PackingBagDto? Bag { get; set; }
}

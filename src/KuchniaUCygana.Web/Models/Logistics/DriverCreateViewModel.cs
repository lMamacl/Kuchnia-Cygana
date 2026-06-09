using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Web.Models.Logistics;

public sealed class DriverCreateViewModel
{
    public CreateDriverRequest Driver { get; set; } = new();
    public IReadOnlyList<DriverUserOptionDto> AvailableUsers { get; set; } = [];
}

using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Web.Models.Logistics;

public sealed class DriverEditViewModel
{
    public UpdateDriverRequest Driver { get; set; } = new();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

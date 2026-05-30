namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class FefoReportFilterDto
{
    public string? Search { get; set; }

    public string? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}

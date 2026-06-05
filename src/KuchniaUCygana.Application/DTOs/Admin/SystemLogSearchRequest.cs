namespace KuchniaUCygana.Application.DTOs.Admin;

public sealed class SystemLogSearchRequest
{
    public string? Search { get; set; }

    public int? UserId { get; set; }

    public string? Action { get; set; }

    public string? TargetEntity { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}

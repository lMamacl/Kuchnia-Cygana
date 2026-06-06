namespace KuchniaUCygana.Application.DTOs.Admin;

public sealed class SystemLogDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? UserFullName { get; set; }

    public string Action { get; set; } = string.Empty;

    public string TargetEntity { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string? IPAddress { get; set; }
}

using KuchniaUCygana.Domain.Entities.Admin;

namespace KuchniaUCygana.Domain.Interfaces;

public interface ISystemLogRepository : IRepository<SystemLog>
{
    Task<SystemLogSearchResult> SearchAsync(SystemLogSearchQuery query);

    Task<IReadOnlyList<SystemLogRow>> GetLatestAsync(int limit);

    Task<IReadOnlyList<SystemLogRow>> GetByUserAsync(int userId, int limit);

    Task<IReadOnlyList<SystemLogRow>> GetByTargetAsync(string targetEntity, string targetId, int limit);

    Task<SystemLogRow?> GetRowByIdAsync(int id);

    Task<int> ArchiveOldLogsAsync(int olderThanDays, int batchSize);
}

public sealed class SystemLogSearchQuery
{
    public string? Search { get; init; }

    public int? UserId { get; init; }

    public string? Action { get; init; }

    public string? TargetEntity { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? ToExclusive { get; init; }

    public bool IncludeArchived { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;
}

public sealed class SystemLogSearchResult
{
    public IReadOnlyList<SystemLogRow> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public IReadOnlyList<string> Actions { get; init; } = [];

    public IReadOnlyList<string> TargetEntities { get; init; } = [];
}

public sealed class SystemLogRow
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

    public bool IsArchived { get; set; }
}

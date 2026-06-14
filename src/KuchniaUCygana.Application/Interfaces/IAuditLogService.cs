using KuchniaUCygana.Application.DTOs.Admin;

namespace KuchniaUCygana.Application.Interfaces;

public interface IAuditLogService
{
    Task<IEnumerable<SystemLogDto>> GetSystemLogsAsync();

    Task<SystemLogPageDto> SearchSystemLogsAsync(SystemLogSearchRequest request);

    Task<IEnumerable<SystemLogDto>> GetSystemLogsByUserAsync(int userId);

    Task<IEnumerable<SystemLogDto>> GetSystemLogsByTargetAsync(string targetEntity, string targetId);

    Task<SystemLogDto?> GetSystemLogByIdAsync(int id);

    Task<SystemLogDto> CreateSystemLogAsync(CreateSystemLogRequest request);

    Task<int> ArchiveLogsAsync(int olderThanDays, int batchSize);
}

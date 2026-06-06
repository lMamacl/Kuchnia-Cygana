using AutoMapper;
using KuchniaUCygana.Application.DTOs.Admin;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class AuditLogService : IAuditLogService
{
    private const int DefaultSearchDays = 30;

    private readonly ISystemLogRepository systemLogRepository;
    private readonly IRepository<User> userRepository;
    private readonly IMapper mapper;

    public AuditLogService(
        ISystemLogRepository systemLogRepository,
        IRepository<User> userRepository,
        IMapper mapper)
    {
        this.systemLogRepository = systemLogRepository;
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    public async Task<IEnumerable<SystemLogDto>> GetSystemLogsAsync()
    {
        var logs = await systemLogRepository.GetLatestAsync(50);
        return logs.Select(MapRow).ToArray();
    }

    public async Task<SystemLogPageDto> SearchSystemLogsAsync(SystemLogSearchRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 10, 100);
        var from = request.From?.Date ?? DateTime.Today.AddDays(-DefaultSearchDays);
        var result = await systemLogRepository.SearchAsync(new SystemLogSearchQuery
        {
            Search = request.Search,
            UserId = request.UserId,
            Action = request.Action,
            TargetEntity = request.TargetEntity,
            From = ToDateTimeOffset(from),
            ToExclusive = request.To.HasValue
                ? ToDateTimeOffset(request.To.Value.Date.AddDays(1))
                : null,
            IncludeArchived = request.IncludeArchived,
            Page = page,
            PageSize = pageSize,
        });

        var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));

        return new SystemLogPageDto
        {
            Items = result.Items.Select(MapRow).ToArray(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = totalPages,
            Actions = result.Actions,
            TargetEntities = result.TargetEntities,
        };
    }

    public async Task<IEnumerable<SystemLogDto>> GetSystemLogsByUserAsync(int userId)
    {
        var logs = await systemLogRepository.GetByUserAsync(userId, 500);
        return logs.Select(MapRow).ToArray();
    }

    public async Task<IEnumerable<SystemLogDto>> GetSystemLogsByTargetAsync(string targetEntity, string targetId)
    {
        var logs = await systemLogRepository.GetByTargetAsync(targetEntity, targetId, 500);
        return logs.Select(MapRow).ToArray();
    }

    public async Task<SystemLogDto?> GetSystemLogByIdAsync(int id)
    {
        var log = await systemLogRepository.GetRowByIdAsync(id);
        return log is null ? null : MapRow(log);
    }

    public async Task<SystemLogDto> CreateSystemLogAsync(CreateSystemLogRequest request)
    {
        await EnsureUserExistsAsync(request.UserId);

        var log = mapper.Map<SystemLog>(request);
        var id = await systemLogRepository.InsertAsync(log);
        log.Id = id;

        var row = await systemLogRepository.GetRowByIdAsync(id);
        return row is null ? mapper.Map<SystemLogDto>(log) : MapRow(row);
    }

    private async Task EnsureUserExistsAsync(int userId)
    {
        if (await userRepository.GetByIdAsync(userId) is null)
        {
            throw new InvalidOperationException($"Uzytkownik o ID {userId} nie istnieje.");
        }
    }

    private static SystemLogDto MapRow(SystemLogRow row)
    {
        return new SystemLogDto
        {
            Id = row.Id,
            UserId = row.UserId,
            UserFullName = row.UserFullName,
            Action = row.Action,
            TargetEntity = row.TargetEntity,
            TargetId = row.TargetId,
            OldValue = row.OldValue,
            NewValue = row.NewValue,
            Timestamp = row.Timestamp,
            IPAddress = row.IPAddress,
            IsArchived = row.IsArchived,
        };
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Local));
    }
}

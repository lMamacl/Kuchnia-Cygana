using AutoMapper;
using KuchniaUCygana.Application.DTOs.Admin;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly IRepository<SystemLog> systemLogRepository;
    private readonly IRepository<User> userRepository;
    private readonly IMapper mapper;

    public AuditLogService(
        IRepository<SystemLog> systemLogRepository,
        IRepository<User> userRepository,
        IMapper mapper)
    {
        this.systemLogRepository = systemLogRepository;
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    public async Task<IEnumerable<SystemLogDto>> GetSystemLogsAsync()
    {
        var logs = await systemLogRepository.GetAllAsync();
        return await MapSystemLogsAsync(logs.OrderByDescending(log => log.Timestamp));
    }

    public async Task<SystemLogPageDto> SearchSystemLogsAsync(SystemLogSearchRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 10, 100);
        var logs = await MapSystemLogsAsync(
            (await systemLogRepository.GetAllAsync()).OrderByDescending(log => log.Timestamp));

        var actions = logs
            .Select(log => log.Action)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToArray();
        var targetEntities = logs
            .Select(log => log.TargetEntity)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToArray();

        IEnumerable<SystemLogDto> query = logs;

        if (request.UserId is > 0)
        {
            query = query.Where(log => log.UserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(log => string.Equals(log.Action, request.Action.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.TargetEntity))
        {
            query = query.Where(log => string.Equals(log.TargetEntity, request.TargetEntity.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (request.From.HasValue)
        {
            var from = request.From.Value.Date;
            query = query.Where(log => log.Timestamp.LocalDateTime >= from);
        }

        if (request.To.HasValue)
        {
            var toExclusive = request.To.Value.Date.AddDays(1);
            query = query.Where(log => log.Timestamp.LocalDateTime < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(log =>
                Contains(log.Action, search) ||
                Contains(log.TargetEntity, search) ||
                Contains(log.TargetId, search) ||
                Contains(log.UserFullName, search) ||
                Contains(log.IPAddress, search) ||
                Contains(log.OldValue, search) ||
                Contains(log.NewValue, search));
        }

        var filtered = query.ToArray();
        var totalCount = filtered.Length;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        return new SystemLogPageDto
        {
            Items = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Actions = actions,
            TargetEntities = targetEntities,
        };
    }

    public async Task<IEnumerable<SystemLogDto>> GetSystemLogsByUserAsync(int userId)
    {
        var logs = await systemLogRepository.GetAllAsync();
        return await MapSystemLogsAsync(logs
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.Timestamp));
    }

    public async Task<IEnumerable<SystemLogDto>> GetSystemLogsByTargetAsync(string targetEntity, string targetId)
    {
        var logs = await systemLogRepository.GetAllAsync();
        return await MapSystemLogsAsync(logs
            .Where(log =>
                log.TargetEntity.Equals(targetEntity, StringComparison.OrdinalIgnoreCase) &&
                log.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(log => log.Timestamp));
    }

    public async Task<SystemLogDto?> GetSystemLogByIdAsync(int id)
    {
        var log = await systemLogRepository.GetByIdAsync(id);
        if (log is null)
        {
            return null;
        }

        return (await MapSystemLogsAsync(new[] { log })).Single();
    }

    public async Task<SystemLogDto> CreateSystemLogAsync(CreateSystemLogRequest request)
    {
        await EnsureUserExistsAsync(request.UserId);

        var log = mapper.Map<SystemLog>(request);
        var id = await systemLogRepository.InsertAsync(log);
        log.Id = id;

        return (await MapSystemLogsAsync(new[] { log })).Single();
    }

    private async Task<List<SystemLogDto>> MapSystemLogsAsync(IEnumerable<SystemLog> logs)
    {
        var list = mapper.Map<List<SystemLogDto>>(logs);
        var users = (await userRepository.GetAllAsync()).ToDictionary(user => user.Id);

        foreach (var log in list)
        {
            if (users.TryGetValue(log.UserId, out var user))
            {
                log.UserFullName = BuildUserFullName(user);
            }
        }

        return list;
    }

    private async Task EnsureUserExistsAsync(int userId)
    {
        if (await userRepository.GetByIdAsync(userId) is null)
        {
            throw new InvalidOperationException($"Uzytkownik o ID {userId} nie istnieje.");
        }
    }

    private static string BuildUserFullName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }
}

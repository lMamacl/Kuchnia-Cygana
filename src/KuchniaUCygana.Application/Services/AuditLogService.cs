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
}

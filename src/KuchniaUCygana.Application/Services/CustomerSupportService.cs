using AutoMapper;
using KuchniaUCygana.Application.DTOs.CustomerService;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class CustomerSupportService : ICustomerSupportService
{
    private readonly ITicketRepository ticketRepository;
    private readonly IRepository<TicketAttachment> ticketAttachmentRepository;
    private readonly IRepository<SystemLog> systemLogRepository;
    private readonly IRepository<User> userRepository;
    private readonly IMapper mapper;

    public CustomerSupportService(
        ITicketRepository ticketRepository,
        IRepository<TicketAttachment> ticketAttachmentRepository,
        IRepository<SystemLog> systemLogRepository,
        IRepository<User> userRepository,
        IMapper mapper)
    {
        this.ticketRepository = ticketRepository;
        this.ticketAttachmentRepository = ticketAttachmentRepository;
        this.systemLogRepository = systemLogRepository;
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    public async Task<IEnumerable<TicketDto>> GetTicketsAsync()
    {
        var tickets = await ticketRepository.GetAllAsync();
        return await MapTicketsAsync(tickets);
    }

    public async Task<IEnumerable<TicketDto>> GetOpenTicketsAsync()
    {
        var tickets = await ticketRepository.GetOpenTicketsAsync();
        return await MapTicketsAsync(tickets);
    }

    public async Task<IEnumerable<TicketDto>> GetTicketsByClientIdAsync(int clientUserId)
    {
        var tickets = await ticketRepository.GetByClientIdAsync(clientUserId);
        return await MapTicketsAsync(tickets);
    }

    public async Task<IEnumerable<TicketDto>> GetTicketsByStatusAsync(TicketStatus status)
    {
        var tickets = await ticketRepository.GetByStatusAsync(status);
        return await MapTicketsAsync(tickets);
    }

    public async Task<TicketDto?> GetTicketByIdAsync(int id)
    {
        var ticket = await ticketRepository.GetByIdAsync(id);
        if (ticket is null)
        {
            return null;
        }

        return (await MapTicketsAsync(new[] { ticket })).Single();
    }

    public async Task<TicketDto> CreateTicketAsync(CreateTicketRequest request)
    {
        await EnsureUserExistsAsync(request.ClientUserId);

        var ticket = mapper.Map<Ticket>(request);
        var id = await ticketRepository.InsertAsync(ticket);
        ticket.Id = id;

        return (await MapTicketsAsync(new[] { ticket })).Single();
    }

    public async Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketRequest request)
    {
        var existing = await ticketRepository.GetByIdAsync(id);
        if (existing is null)
        {
            return null;
        }

        if (request.AssignedToUserId.HasValue)
        {
            await EnsureUserExistsAsync(request.AssignedToUserId.Value);
        }

        request.Id = id;
        mapper.Map(request, existing);
        ApplyClosedAt(existing);
        await ticketRepository.UpdateAsync(existing);

        return (await MapTicketsAsync(new[] { existing })).Single();
    }

    public async Task<TicketDto?> AssignTicketAsync(AssignTicketRequest request)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId);
        if (ticket is null)
        {
            return null;
        }

        await EnsureUserExistsAsync(request.AssignedToUserId);

        ticket.AssignedToUserId = request.AssignedToUserId;
        if (ticket.Status == TicketStatus.New)
        {
            ticket.Status = TicketStatus.Open;
        }

        ApplyClosedAt(ticket);
        await ticketRepository.UpdateAsync(ticket);

        return (await MapTicketsAsync(new[] { ticket })).Single();
    }

    public async Task<TicketDto?> ChangeTicketStatusAsync(ChangeTicketStatusRequest request)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId);
        if (ticket is null)
        {
            return null;
        }

        ticket.Status = request.Status;
        ApplyClosedAt(ticket);
        await ticketRepository.UpdateAsync(ticket);

        return (await MapTicketsAsync(new[] { ticket })).Single();
    }

    public async Task<bool> DeleteTicketAsync(int id)
    {
        return await ticketRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<TicketAttachmentDto>> GetTicketAttachmentsAsync(int ticketId)
    {
        var attachments = await ticketAttachmentRepository.GetAllAsync();
        return await MapTicketAttachmentsAsync(attachments
            .Where(attachment => attachment.TicketId == ticketId)
            .OrderByDescending(attachment => attachment.UploadedAt));
    }

    public async Task<TicketAttachmentDto?> GetTicketAttachmentByIdAsync(int id)
    {
        var attachment = await ticketAttachmentRepository.GetByIdAsync(id);
        if (attachment is null)
        {
            return null;
        }

        return (await MapTicketAttachmentsAsync(new[] { attachment })).Single();
    }

    public async Task<TicketAttachmentDto> AddTicketAttachmentAsync(CreateTicketAttachmentRequest request)
    {
        await EnsureTicketExistsAsync(request.TicketId);
        await EnsureUserExistsAsync(request.UploadedByUserId);

        var attachment = mapper.Map<TicketAttachment>(request);
        var id = await ticketAttachmentRepository.InsertAsync(attachment);
        attachment.Id = id;

        return (await MapTicketAttachmentsAsync(new[] { attachment })).Single();
    }

    public async Task<bool> DeleteTicketAttachmentAsync(int id)
    {
        return await ticketAttachmentRepository.DeleteAsync(id);
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

    private async Task<List<TicketDto>> MapTicketsAsync(IEnumerable<Ticket> tickets)
    {
        var list = mapper.Map<List<TicketDto>>(tickets);
        var users = (await userRepository.GetAllAsync()).ToDictionary(user => user.Id);

        foreach (var ticket in list)
        {
            if (users.TryGetValue(ticket.ClientUserId, out var client))
            {
                ticket.ClientFullName = BuildUserFullName(client);
            }

            if (ticket.AssignedToUserId.HasValue &&
                users.TryGetValue(ticket.AssignedToUserId.Value, out var assigned))
            {
                ticket.AssignedToFullName = BuildUserFullName(assigned);
            }
        }

        return list;
    }

    private async Task<List<TicketAttachmentDto>> MapTicketAttachmentsAsync(
        IEnumerable<TicketAttachment> attachments)
    {
        var list = mapper.Map<List<TicketAttachmentDto>>(attachments);
        var users = (await userRepository.GetAllAsync()).ToDictionary(user => user.Id);

        foreach (var attachment in list)
        {
            if (users.TryGetValue(attachment.UploadedByUserId, out var user))
            {
                attachment.UploadedByFullName = BuildUserFullName(user);
            }
        }

        return list;
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

    private async Task EnsureTicketExistsAsync(int ticketId)
    {
        if (await ticketRepository.GetByIdAsync(ticketId) is null)
        {
            throw new InvalidOperationException($"Zgloszenie o ID {ticketId} nie istnieje.");
        }
    }

    private async Task EnsureUserExistsAsync(int userId)
    {
        if (await userRepository.GetByIdAsync(userId) is null)
        {
            throw new InvalidOperationException($"Uzytkownik o ID {userId} nie istnieje.");
        }
    }

    private static void ApplyClosedAt(Ticket ticket)
    {
        ticket.ClosedAt = ticket.Status is TicketStatus.Resolved or TicketStatus.Closed
            ? ticket.ClosedAt ?? DateTimeOffset.UtcNow
            : null;
    }

    private static string BuildUserFullName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}

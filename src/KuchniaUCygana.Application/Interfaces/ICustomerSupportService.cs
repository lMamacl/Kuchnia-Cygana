using KuchniaUCygana.Application.DTOs.CustomerService;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.Interfaces;

public interface ICustomerSupportService
{
    Task<IEnumerable<TicketDto>> GetTicketsAsync();

    Task<IEnumerable<TicketDto>> GetOpenTicketsAsync();

    Task<IEnumerable<TicketDto>> GetTicketsByClientIdAsync(int clientUserId);

    Task<IEnumerable<TicketDto>> GetTicketsByStatusAsync(TicketStatus status);

    Task<TicketDto?> GetTicketByIdAsync(int id);

    Task<TicketDto> CreateTicketAsync(CreateTicketRequest request);

    Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketRequest request);

    Task<TicketDto?> AssignTicketAsync(AssignTicketRequest request);

    Task<TicketDto?> ChangeTicketStatusAsync(ChangeTicketStatusRequest request);

    Task<bool> DeleteTicketAsync(int id);

    Task<IEnumerable<TicketAttachmentDto>> GetTicketAttachmentsAsync(int ticketId);

    Task<TicketAttachmentDto?> GetTicketAttachmentByIdAsync(int id);

    Task<TicketAttachmentDto> AddTicketAttachmentAsync(CreateTicketAttachmentRequest request);

    Task<bool> DeleteTicketAttachmentAsync(int id);

    Task<IEnumerable<SystemLogDto>> GetSystemLogsAsync();

    Task<IEnumerable<SystemLogDto>> GetSystemLogsByUserAsync(int userId);

    Task<IEnumerable<SystemLogDto>> GetSystemLogsByTargetAsync(string targetEntity, string targetId);

    Task<SystemLogDto?> GetSystemLogByIdAsync(int id);

    Task<SystemLogDto> CreateSystemLogAsync(CreateSystemLogRequest request);
}

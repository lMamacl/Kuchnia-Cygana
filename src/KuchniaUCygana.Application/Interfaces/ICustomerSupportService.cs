using KuchniaUCygana.Application.DTOs.CustomerService;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.Interfaces;

public interface ICustomerSupportService
{
    Task<TicketPageDto> SearchTicketsAsync(TicketSearchRequest request);

    Task<TicketDashboardSummaryDto> GetTicketDashboardSummaryAsync();

    Task<IEnumerable<TicketDto>> GetTicketsByClientIdAsync(int clientUserId);

    Task<IEnumerable<TicketDto>> GetTicketsByStatusAsync(TicketStatus status);

    Task<TicketDto?> GetTicketByIdAsync(int id);

    Task<IReadOnlyDictionary<int, TicketOperationalContextDto>> GetOperationalContextsAsync(IEnumerable<TicketDto> tickets);

    Task<IReadOnlyList<TicketDeliveryOptionDto>> GetDeliveryOptionsAsync(DateTime fromInclusive, DateTime toInclusive);

    Task<TicketDto> CreateTicketAsync(CreateTicketRequest request);

    Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketRequest request);

    Task<TicketDto?> AssignTicketAsync(AssignTicketRequest request);

    Task<TicketDto?> ChangeTicketStatusAsync(ChangeTicketStatusRequest request);

    Task<bool> DeleteTicketAsync(int id);

    Task<IEnumerable<TicketAttachmentDto>> GetTicketAttachmentsAsync(int ticketId);

    Task<TicketAttachmentDto?> GetTicketAttachmentByIdAsync(int id);

    Task<TicketAttachmentDto> AddTicketAttachmentAsync(CreateTicketAttachmentRequest request);

    Task<bool> DeleteTicketAttachmentAsync(int id);
}

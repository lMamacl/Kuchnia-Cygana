using KuchniaUCygana.Domain.Entities.Admin;

namespace KuchniaUCygana.Domain.Interfaces;

public interface ITicketAttachmentRepository : IRepository<TicketAttachment>
{
    Task<IReadOnlyList<TicketAttachment>> GetByTicketIdAsync(int ticketId);
}

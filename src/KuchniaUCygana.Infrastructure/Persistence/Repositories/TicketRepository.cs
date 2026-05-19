using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class TicketRepository : BaseRepository<Ticket>, ITicketRepository
{
    public TicketRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<Ticket>> GetByClientIdAsync(int clientUserId)
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync<Ticket>(t => t.ClientUserId == clientUserId && !t.IsDeleted);
    }

    public async Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status)
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync<Ticket>(t => t.Status == status && !t.IsDeleted);
    }

    public async Task<IEnumerable<Ticket>> GetOpenTicketsAsync()
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync<Ticket>(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved && !t.IsDeleted);
    }
}
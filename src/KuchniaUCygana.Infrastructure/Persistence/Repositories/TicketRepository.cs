using Dapper;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class TicketRepository : BaseRepository<Ticket>, ITicketRepository
{
    public TicketRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<Ticket>> GetByClientIdAsync(int clientUserId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<Ticket>(
            "SELECT * FROM [Tickets] WHERE [ClientUserId] = @ClientUserId AND [IsDeleted] = 0", 
            new { ClientUserId = clientUserId });
    }

    public async Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<Ticket>(
            "SELECT * FROM [Tickets] WHERE [Status] = @Status AND [IsDeleted] = 0", 
            new { Status = (int)status });
    }

    public async Task<IEnumerable<Ticket>> GetOpenTicketsAsync()
    {
        using var db = Factory.CreateConnection();
        var sql = "SELECT * FROM [Tickets] WHERE [Status] NOT IN (@Closed, @Resolved) AND [IsDeleted] = 0";
        return await db.QueryAsync<Ticket>(sql, new { Closed = (int)TicketStatus.Closed, Resolved = (int)TicketStatus.Resolved });
    }
}
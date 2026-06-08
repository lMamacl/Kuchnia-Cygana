using Dapper;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class TicketAttachmentRepository : BaseRepository<TicketAttachment>, ITicketAttachmentRepository
{
    public TicketAttachmentRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null)
        : base(factory, currentUserService)
    {
    }

    public async Task<IReadOnlyList<TicketAttachment>> GetByTicketIdAsync(int ticketId)
    {
        using var db = Factory.CreateConnection();
        var rows = await db.QueryAsync<TicketAttachment>(
            """
            SELECT *
            FROM [TicketAttachments]
            WHERE [IsDeleted] = 0
              AND [TicketId] = @TicketId
            ORDER BY [UploadedAt] DESC, [Id] DESC;
            """,
            new { TicketId = ticketId });

        return rows.ToList();
    }
}

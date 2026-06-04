using Dapper;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Production;

public sealed class BoxLabelRepository : BaseRepository<BoxLabel>, IBoxLabelRepository
{
    public BoxLabelRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<BoxLabel?> GetLatestForPackingItemAsync(int packingItemId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<BoxLabel>(
            """
            SELECT TOP 1 *
            FROM BoxLabels
            WHERE PackingItemId = @packingItemId
            ORDER BY PrintNumber DESC, Id DESC;
            """,
            new { packingItemId });
    }

    public async Task<BoxLabel?> GetByQrCodeAsync(string qrCode)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<BoxLabel>(
            """
            SELECT TOP 1 *
            FROM BoxLabels
            WHERE QrCode = @qrCode
            ORDER BY PrintNumber DESC, Id DESC;
            """,
            new { qrCode });
    }

    public async Task<int> GetPrintCountAsync(int packingItemId)
    {
        using var db = Factory.CreateConnection();
        return await db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM BoxLabels
            WHERE PackingItemId = @packingItemId;
            """,
            new { packingItemId });
    }
}

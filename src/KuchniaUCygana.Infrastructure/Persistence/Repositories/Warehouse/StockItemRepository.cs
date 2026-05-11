using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

/// <summary>
/// Repozytorium StockItem z metodami Smart Inventory.
/// </summary>
public sealed class StockItemRepository : BaseRepository<StockItem>, IStockItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StockItemRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StockItem>> GetBelowMinimumAsync()
    {
        using var db = _connectionFactory.CreateConnection();

        // Pobierz składniki, których łączny stan aktywnych partii < MinimumLevel
        // Uwaga: sprawdzamy MinimumLevel > 0 (tylko te z ustawionym progiem)
        var items = await db.SelectAsync<StockItem>(
            si => !si.IsDeleted && si.MinimumLevel > 0);

        var result = new List<StockItem>();

        foreach (var item in items)
        {
            // Sumuj aktywne partie
            var totalQty = await db.ScalarAsync<decimal>(
                db.From<Batch>()
                    .Where(b => b.StockItemId == item.Id && !b.IsDepleted)
                    .Select(b => Sql.Sum(b.CurrentQuantity)));

            if (totalQty < item.MinimumLevel)
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<StockItem?> GetByIngredientIdAsync(int baseIngredientId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.SingleAsync<StockItem>(
            si => si.BaseIngredientId == baseIngredientId && !si.IsDeleted);
    }
}

using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class AddressRepository : BaseRepository<Address>, IAddressRepository
{
    public AddressRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<Address>> GetByUserIdAsync(int userId)
{
        using var db = Factory.CreateConnection();
        const string sql = @"
        SELECT * FROM [Addresses]
        WHERE UserId = @UserId AND IsDeleted = 0
        ORDER BY IsDefault DESC, Label";

        return await db.QueryAsync<Address>(sql, new { UserId = userId });
    }

    public async Task<Address?> GetDefaultByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        var sql = @"
            SELECT * FROM [Addresses] 
            WHERE [UserId] = @UserId 
            AND [IsDefault] = 1 
            AND [IsDeleted] = 0";
        
        return await db.QueryFirstOrDefaultAsync<Address>(sql, new { UserId = userId });
    }

    public async Task SetDefaultAsync(int userId, int addressId)
    {
        using var db = Factory.CreateConnection();
        using var transaction = db.BeginTransaction();
        
        try
        {
            // 1. Resetuj wszystkie obecne domyślne adresy użytkownika
            var resetSql = @"
                UPDATE [Addresses] 
                SET [IsDefault] = 0, [UpdatedAt] = @UpdatedAt 
                WHERE [UserId] = @UserId AND [IsDefault] = 1 AND [IsDeleted] = 0";
            
            await db.ExecuteAsync(resetSql, new { UserId = userId, UpdatedAt = DateTimeOffset.UtcNow }, transaction);
            
            // 2. Ustaw nowy adres jako domyślny
            var setDefaultSql = @"
                UPDATE [Addresses] 
                SET [IsDefault] = 1, [UpdatedAt] = @UpdatedAt 
                WHERE [Id] = @AddressId AND [UserId] = @UserId AND [IsDeleted] = 0";
            
            var rowsAffected = await db.ExecuteAsync(setDefaultSql, new { AddressId = addressId, UserId = userId, UpdatedAt = DateTimeOffset.UtcNow }, transaction);
            
            // 3. Jeśli żaden wiersz nie został zaktualizowany (adres nie istnieje lub nie należy do użytkownika), możesz rzucić wyjątek lub zignorować
            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Address with ID {addressId} not found or does not belong to user {userId}");
            }
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<Address>> GetPendingAddressesAsync()
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<Address>(
            "SELECT * FROM [Addresses] WHERE ([Latitude] IS NULL OR [Longitude] IS NULL) AND [IsDeleted] = 0");
    }
}

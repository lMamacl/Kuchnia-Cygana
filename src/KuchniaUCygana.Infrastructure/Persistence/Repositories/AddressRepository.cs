using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class AddressRepository : BaseRepository<Address>, IAddressRepository
{
    public AddressRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<Address>> GetByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        var q = db.From<Address>()
            .Where(x => x.UserId == userId && x.IsDeleted == false)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Label);
        return await db.SelectAsync(q);
    }

    public async Task<Address?> GetDefaultByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        return await db.SingleAsync<Address>(x =>
            x.UserId == userId && x.IsDefault == true && x.IsDeleted == false);
    }

    public async Task SetDefaultAsync(int userId, int addressId)
    {
        using var db = Factory.CreateConnection();

        var currentDefaults = await db.SelectAsync<Address>(x =>
            x.UserId == userId && x.IsDefault == true && x.IsDeleted == false);

        foreach (var addr in currentDefaults)
        {
            addr.IsDefault = false;
            addr.UpdatedAt = DateTimeOffset.UtcNow;
            await db.UpdateAsync(addr);
        }

        var newDefault = await db.SingleAsync<Address>(x =>
            x.Id == addressId && x.UserId == userId && x.IsDeleted == false);

        if (newDefault is not null)
        {
            newDefault.IsDefault = true;
            newDefault.UpdatedAt = DateTimeOffset.UtcNow;
            await db.UpdateAsync(newDefault);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public class BaseRepository<T> : IRepository<T>
    where T : BaseEntity
{
    protected readonly IDbConnectionFactory Factory;

    public BaseRepository(IDbConnectionFactory factory)
    {
        Factory = factory;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var db = Factory.CreateConnection();
        return await db.DeleteByIdAsync<T>(id) > 0;
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync(predicate);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync<T>();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        using var db = Factory.CreateConnection();
        return await db.SingleByIdAsync<T>(id);
    }

    public async Task<int> InsertAsync(T entity)
    {
        entity.CreatedAt = DateTimeOffset.UtcNow;
        using var db = Factory.CreateConnection();
        return (int)await db.InsertAsync(entity, selectIdentity: true);
    }

    public async Task<bool> UpdateAsync(T entity)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        using var db = Factory.CreateConnection();
        return await db.UpdateAsync(entity) > 0;
    }
}

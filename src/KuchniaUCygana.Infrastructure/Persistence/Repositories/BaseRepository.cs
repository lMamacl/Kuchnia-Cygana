using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Common.Interfaces;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generyczne repozytorium bazowe oparte na ServiceStack.OrmLite.
/// Obsługuje dowolny typ klucza głównego (int, long, Guid...).
/// Automatycznie realizuje Soft Delete dla encji implementujących ISoftDeletable.
/// </summary>
public class BaseRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
{
    protected readonly IDbConnectionFactory Factory;

    public BaseRepository(IDbConnectionFactory factory)
    {
        Factory = factory;
    }

    public virtual async Task<TEntity?> GetByIdAsync(TId id)
    {
        using var db = Factory.CreateConnection();
        var entity = await db.SingleByIdAsync<TEntity>(id);

        // Filtrowanie Soft Delete – nie zwracaj "usuniętych" rekordów
        if (entity is ISoftDeletable softDeletable && softDeletable.IsDeleted)
        {
            return null;
        }

        return entity;
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        using var db = Factory.CreateConnection();

        // Jeśli encja jest ISoftDeletable, automatycznie filtruj usunięte
        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            return await db.SelectAsync<TEntity>(q =>
                ((ISoftDeletable)(object)q).IsDeleted == false);
        }

        return await db.SelectAsync<TEntity>();
    }

    public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync(predicate);
    }

    public virtual async Task<TId> InsertAsync(TEntity entity)
    {
        entity.CreatedAt = DateTimeOffset.UtcNow;

        using var db = Factory.CreateConnection();
        var insertedId = await db.InsertAsync(entity, selectIdentity: true);

        // Rzutowanie na właściwy typ klucza
        return (TId)Convert.ChangeType(insertedId, typeof(TId));
    }

    public virtual async Task<bool> UpdateAsync(TEntity entity)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        using var db = Factory.CreateConnection();
        return await db.UpdateAsync(entity) > 0;
    }

    public virtual async Task<bool> DeleteAsync(TId id)
    {
        using var db = Factory.CreateConnection();
        var entity = await db.SingleByIdAsync<TEntity>(id);

        if (entity is null)
        {
            return false;
        }

        // Soft Delete: zamień flagę zamiast kasować wiersz
        if (entity is ISoftDeletable softDeletable)
        {
            softDeletable.IsDeleted = true;
            softDeletable.DeletedAt = DateTimeOffset.UtcNow;
            return await db.UpdateAsync(entity) > 0;
        }

        // Hard Delete: tylko dla encji bez ISoftDeletable (np. InventoryTransaction)
        return await db.DeleteByIdAsync<TEntity>(id) > 0;
    }
}

/// <summary>
/// Alias dla encji z kluczem int (wsteczna kompatybilność z istniejącym kodem).
/// </summary>
public class BaseRepository<TEntity> : BaseRepository<TEntity, int>, IRepository<TEntity>
    where TEntity : BaseEntity<int>
{
    public BaseRepository(IDbConnectionFactory factory) : base(factory)
    {
    }
}

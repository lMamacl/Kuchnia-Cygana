using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using Dapper;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Common.Interfaces;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generyczne repozytorium bazowe oparte na Dapperze i jawnie generowanym SQL.
/// </summary>
public class BaseRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
{
    private static readonly EntityMetadata Metadata = EntityMetadata.Create(typeof(TEntity));

    protected readonly IDbConnectionFactory Factory;

    public BaseRepository(IDbConnectionFactory factory)
    {
        Factory = factory;
    }

    public virtual async Task<TEntity?> GetByIdAsync(TId id)
    {
        using var db = Factory.CreateConnection();
        var entity = await db.QuerySingleOrDefaultAsync<TEntity>(
            $"SELECT * FROM {Metadata.TableName} WHERE {Metadata.KeyColumn} = @id;",
            new { id });

        if (entity is ISoftDeletable { IsDeleted: true })
        {
            return null;
        }

        return entity;
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        using var db = Factory.CreateConnection();
        var where = typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity))
            ? " WHERE [IsDeleted] = 0"
            : string.Empty;

        return await db.QueryAsync<TEntity>($"SELECT * FROM {Metadata.TableName}{where};");
    }

    public virtual async Task<TId> InsertAsync(TEntity entity)
    {
        entity.CreatedAt = DateTimeOffset.UtcNow;

        using var db = Factory.CreateConnection();
        var insertedId = await db.QuerySingleAsync<object>(
            $"""
            INSERT INTO {Metadata.TableName} ({Metadata.InsertColumns})
            OUTPUT INSERTED.{Metadata.KeyColumn}
            VALUES ({Metadata.InsertParameters});
            """,
            entity);

        return (TId)Convert.ChangeType(
            insertedId,
            Nullable.GetUnderlyingType(typeof(TId)) ?? typeof(TId),
            CultureInfo.InvariantCulture);
    }

    public virtual async Task<bool> UpdateAsync(TEntity entity)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        using var db = Factory.CreateConnection();
        var affected = await db.ExecuteAsync(
            $"""
            UPDATE {Metadata.TableName}
            SET {Metadata.UpdateAssignments}
            WHERE {Metadata.KeyColumn} = @Id;
            """,
            entity);

        return affected > 0;
    }

    public virtual async Task<bool> DeleteAsync(TId id)
    {
        using var db = Factory.CreateConnection();
        var entity = await GetByIdAsync(id);

        if (entity is null)
        {
            return false;
        }

        if (entity is ISoftDeletable softDeletable)
        {
            softDeletable.IsDeleted = true;
            softDeletable.DeletedAt = DateTimeOffset.UtcNow;
            return await UpdateAsync(entity);
        }

        var affected = await db.ExecuteAsync(
            $"DELETE FROM {Metadata.TableName} WHERE {Metadata.KeyColumn} = @id;",
            new { id });

        return affected > 0;
    }

    private sealed record EntityMetadata(
        string TableName,
        string KeyColumn,
        string InsertColumns,
        string InsertParameters,
        string UpdateAssignments)
    {
        public static EntityMetadata Create(Type entityType)
        {
            var table = entityType.GetCustomAttribute<TableAttribute>();
            var tableName = table is null
                ? Quote(entityType.Name)
                : string.IsNullOrWhiteSpace(table.Schema)
                    ? Quote(table.Name)
                    : $"{Quote(table.Schema)}.{Quote(table.Name)}";

            var properties = entityType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<NotMappedAttribute>() is null)
                .ToArray();

            var key = properties.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() is not null)
                ?? properties.FirstOrDefault(p => p.Name == "Id")
                ?? throw new InvalidOperationException($"Entity {entityType.Name} does not define a key property.");

            var writable = properties.Where(p => p != key).ToArray();
            if (writable.Length == 0)
            {
                throw new InvalidOperationException($"Entity {entityType.Name} does not define writable columns.");
            }

            return new EntityMetadata(
                tableName,
                Quote(key.Name),
                string.Join(", ", writable.Select(p => Quote(p.Name))),
                string.Join(", ", writable.Select(p => "@" + p.Name)),
                string.Join(", ", writable.Select(p => $"{Quote(p.Name)} = @{p.Name}")));
        }

        private static string Quote(string identifier) => $"[{identifier.Replace("]", "]]")}]";
    }
}

/// <summary>
/// Alias dla encji z kluczem int.
/// </summary>
public class BaseRepository<TEntity> : BaseRepository<TEntity, int>, IRepository<TEntity>
    where TEntity : BaseEntity<int>
{
    public BaseRepository(IDbConnectionFactory factory) : base(factory)
    {
    }
}

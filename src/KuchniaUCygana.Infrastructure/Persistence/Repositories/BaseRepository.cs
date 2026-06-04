using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    /// <summary>
    /// Initializes a new instance of <see cref="BaseRepository{TEntity, TId}"/> with the specified connection factory.
    /// </summary>
    /// <param name="factory">Factory used to create database connections for repository operations.</param>
    public BaseRepository(IDbConnectionFactory factory)
    {
        Factory = factory;
    }

    /// <summary>
    /// Fetches an entity by its primary key, treating soft-deleted entities as missing.
    /// </summary>
    /// <param name="id">The primary key value of the entity to fetch.</param>
    /// <returns>The entity with the specified id, or null if not found or soft-deleted.</returns>
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

    /// <summary>
    /// Retrieves all entities from the repository's table.
    /// </summary>
    /// <returns>All entities from the table; if TEntity implements <see cref="ISoftDeletable"/>, excludes rows with <c>IsDeleted = true</c>.</returns>
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        using var db = Factory.CreateConnection();
        var where = typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity))
            ? " WHERE [IsDeleted] = 0"
            : string.Empty;

        return await db.QueryAsync<TEntity>($"SELECT * FROM {Metadata.TableName}{where};");
    }

    /// <summary>
    /// Inserts the given entity into the database and returns its generated key.
    /// </summary>
    /// <param name="entity">The entity to insert; its CreatedAt is set to the current UTC time before insertion.</param>
    /// <returns>The generated primary key value for the newly inserted entity.</returns>
    public virtual async Task<TId> InsertAsync(TEntity entity)
    {
        entity.CreatedAt = DateTimeOffset.UtcNow;

        using var db = Factory.CreateConnection();
        var insertedId = await db.ExecuteScalarAsync<TId>(
            $"""
            INSERT INTO {Metadata.TableName} ({Metadata.InsertColumns})
            OUTPUT INSERTED.{Metadata.KeyColumn}
            VALUES ({Metadata.InsertParameters});
            """,
            entity);

        return insertedId!;
    }

    /// <summary>
    /// Updates an existing entity in the database and sets its UpdatedAt timestamp to the current UTC time.
    /// </summary>
    /// <param name="entity">The entity to update; its key property must be populated to identify the target row.</param>
    /// <returns>`true` if one or more rows were modified, `false` otherwise.</returns>
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

    /// <summary>
    /// Deletes the entity with the specified key. If the entity implements <see cref="ISoftDeletable"/>, marks it deleted by setting <c>IsDeleted</c> and <c>DeletedAt</c> and persists the change; otherwise performs a physical delete.
    /// </summary>
    /// <param name="id">The primary key of the entity to delete.</param>
    /// <returns><c>true</c> if an entity was deleted or marked deleted and saved; <c>false</c> if no entity with the given key exists.</returns>
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
        /// <summary>
        /// Builds EntityMetadata for the specified entity type, deriving the table name, key column, insert column list, insert parameter placeholders, and update assignments.
        /// </summary>
        /// <param name="entityType">The CLR type of the entity to inspect for mapping attributes and readable/writable properties.</param>
        /// <returns>An EntityMetadata record containing the quoted table name, quoted key column, comma-separated insert columns, insert parameter placeholders, and comma-separated update assignments.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the entity type does not define a key property or does not have any writable columns.</exception>
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

        /// <summary>
/// Wraps a SQL identifier in SQL Server bracket quoting and escapes any ']' characters inside the identifier.
/// </summary>
/// <param name="identifier">An unquoted SQL identifier (e.g., table or column name).</param>
/// <returns>The identifier surrounded by brackets with internal ']' characters doubled (e.g., a]b -> [a]]b]).</returns>
private static string Quote(string identifier) => $"[{identifier.Replace("]", "]]")}]";
    }
}

/// <summary>
/// Alias dla encji z kluczem int.
/// </summary>
public class BaseRepository<TEntity> : BaseRepository<TEntity, int>, IRepository<TEntity>
    where TEntity : BaseEntity<int>
{
    /// <summary>
    /// Initializes a new instance of the repository using the provided database connection factory.
    /// </summary>
    public BaseRepository(IDbConnectionFactory factory) : base(factory)
    {
    }
}

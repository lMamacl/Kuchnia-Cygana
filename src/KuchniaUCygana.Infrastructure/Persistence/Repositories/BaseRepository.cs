using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Common.Interfaces;
using KuchniaUCygana.Domain.Entities.Admin;
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
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    private static readonly string[] SensitiveNameParts =
    [
        "password",
        "passwordhash",
        "token",
        "secret",
        "apikey",
        "api_key",
        "stripe",
        "session",
        "refresh",
        "accesskey",
        "privatekey",
        "cvv",
        "cardnumber",
    ];

    protected readonly IDbConnectionFactory Factory;
    private readonly ICurrentUserService? currentUserService;

    public BaseRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null)
    {
        Factory = factory;
        this.currentUserService = currentUserService;
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
        ApplyCreatedBy(entity);

        using var db = Factory.CreateConnection();
        var insertedId = await db.ExecuteScalarAsync<TId>(
            $"""
            INSERT INTO {Metadata.TableName} ({Metadata.InsertColumns})
            OUTPUT INSERTED.{Metadata.KeyColumn}
            VALUES ({Metadata.InsertParameters});
            """,
            entity);

        entity.Id = insertedId!;
        await WriteAuditLogAsync(db, "Insert", null, entity);
        return insertedId!;
    }

    /// <summary>
    /// Updates an existing entity in the database and sets its UpdatedAt timestamp to the current UTC time.
    /// </summary>
    /// <param name="entity">The entity to update; its key property must be populated to identify the target row.</param>
    /// <returns>`true` if one or more rows were modified, `false` otherwise.</returns>
    public virtual async Task<bool> UpdateAsync(TEntity entity)
    {
        using var db = Factory.CreateConnection();
        var before = await db.QuerySingleOrDefaultAsync<TEntity>(
            $"SELECT * FROM {Metadata.TableName} WHERE {Metadata.KeyColumn} = @Id;",
            new { entity.Id });

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        ApplyUpdatedBy(entity);

        var affected = await db.ExecuteAsync(
            $"""
            UPDATE {Metadata.TableName}
            SET {Metadata.UpdateAssignments}
            WHERE {Metadata.KeyColumn} = @Id;
            """,
            entity);

        if (affected > 0)
        {
            await WriteAuditLogAsync(db, "Update", before, entity);
        }

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
        var entity = await db.QuerySingleOrDefaultAsync<TEntity>(
            $"SELECT * FROM {Metadata.TableName} WHERE {Metadata.KeyColumn} = @Id;",
            new { Id = id });

        if (entity is null || entity is ISoftDeletable { IsDeleted: true })
        {
            return false;
        }

        if (entity is ISoftDeletable softDeletable)
        {
            var oldValueJson = SerializeAuditValue(entity);
            softDeletable.IsDeleted = true;
            softDeletable.DeletedAt = DateTimeOffset.UtcNow;
            ApplyDeletedBy(entity);
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            ApplyUpdatedBy(entity);

            var affected = await db.ExecuteAsync(
                $"""
                UPDATE {Metadata.TableName}
                SET {Metadata.UpdateAssignments}
                WHERE {Metadata.KeyColumn} = @Id;
                """,
                entity);

            if (affected > 0)
            {
                await WriteAuditLogAsync(
                    db,
                    "Delete",
                    entity.Id,
                    oldValueJson,
                    SerializeAuditValue(entity));
            }

            return affected > 0;
        }

        var deleted = await db.ExecuteAsync(
            $"DELETE FROM {Metadata.TableName} WHERE {Metadata.KeyColumn} = @id;",
            new { id });

        if (deleted > 0)
        {
            await WriteAuditLogAsync(db, "Delete", entity, null);
        }

        return deleted > 0;
    }

    private void ApplyCreatedBy(TEntity entity)
    {
        if (entity is AuditableEntity<TId> auditable && string.IsNullOrWhiteSpace(auditable.CreatedBy))
        {
            auditable.CreatedBy = GetActorName();
        }
    }

    private void ApplyUpdatedBy(TEntity entity)
    {
        if (entity is AuditableEntity<TId> auditable)
        {
            auditable.UpdatedBy = GetActorName();
        }
    }

    private void ApplyDeletedBy(TEntity entity)
    {
        if (entity is AuditableEntity<TId> auditable)
        {
            auditable.DeletedBy = GetActorName();
        }
    }

    private string GetActorName()
    {
        return currentUserService?.GetUserName() ?? "System";
    }

    private async Task WriteAuditLogAsync(IDbConnection db, string action, TEntity? oldValue, TEntity? newValue)
    {
        if (!ShouldWriteAuditLog(out var userId) || userId is null)
        {
            return;
        }

        var target = newValue ?? oldValue;
        if (target is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        await db.ExecuteAsync(
            """
            INSERT INTO [SystemLogs]
                ([UserId], [Action], [TargetEntity], [TargetId], [OldValue], [NewValue], [Timestamp], [IPAddress], [CreatedAt])
            VALUES
                (@UserId, @Action, @TargetEntity, @TargetId, @OldValue, @NewValue, @Timestamp, @IPAddress, @CreatedAt);
            """,
            new
            {
                UserId = userId.Value,
                Action = Truncate(action, 100),
                TargetEntity = Truncate(typeof(TEntity).Name, 50),
                TargetId = Truncate(Convert.ToString(target.Id) ?? string.Empty, 100),
                OldValue = SerializeAuditValue(oldValue),
                NewValue = SerializeAuditValue(newValue),
                Timestamp = now,
                IPAddress = Truncate(currentUserService?.GetIpAddress(), 45),
                CreatedAt = now,
            });
    }

    private async Task WriteAuditLogAsync(
        IDbConnection db,
        string action,
        TId targetId,
        string? oldValueJson,
        string? newValueJson)
    {
        if (!ShouldWriteAuditLog(out var userId) || userId is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        await db.ExecuteAsync(
            """
            INSERT INTO [SystemLogs]
                ([UserId], [Action], [TargetEntity], [TargetId], [OldValue], [NewValue], [Timestamp], [IPAddress], [CreatedAt])
            VALUES
                (@UserId, @Action, @TargetEntity, @TargetId, @OldValue, @NewValue, @Timestamp, @IPAddress, @CreatedAt);
            """,
            new
            {
                UserId = userId.Value,
                Action = Truncate(action, 100),
                TargetEntity = Truncate(typeof(TEntity).Name, 50),
                TargetId = Truncate(Convert.ToString(targetId) ?? string.Empty, 100),
                OldValue = oldValueJson,
                NewValue = newValueJson,
                Timestamp = now,
                IPAddress = Truncate(currentUserService?.GetIpAddress(), 45),
                CreatedAt = now,
            });
    }

    private bool ShouldWriteAuditLog(out int? userId)
    {
        userId = currentUserService?.GetUserId();
        return typeof(TEntity) != typeof(SystemLog) &&
            currentUserService?.IsAuthenticated == true &&
            userId is > 0;
    }

    private static string? SerializeAuditValue(TEntity? entity)
    {
        if (entity is null)
        {
            return null;
        }

        var values = Metadata.AuditProperties.ToDictionary(
            property => property.Name,
            property => IsSensitive(property.Name) ? "********" : property.GetValue(entity));

        return JsonSerializer.Serialize(values, AuditJsonOptions);
    }

    private static bool IsSensitive(string propertyName)
    {
        return SensitiveNameParts.Any(part =>
            propertyName.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private sealed record EntityMetadata(
        string TableName,
        string KeyColumn,
        string InsertColumns,
        string InsertParameters,
        string UpdateAssignments,
        IReadOnlyList<PropertyInfo> AuditProperties)
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
                string.Join(", ", writable.Select(p => $"{Quote(p.Name)} = @{p.Name}")),
                properties);
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
    public BaseRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null)
        : base(factory, currentUserService)
    {
    }
}

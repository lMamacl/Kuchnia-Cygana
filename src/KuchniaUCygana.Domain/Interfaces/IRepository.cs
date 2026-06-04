using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Interfaces;

/// <summary>
/// Generyczny interfejs repozytorium obsługujący dowolny typ klucza głównego.
/// </summary>
public interface IRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
{
    /// <summary>
/// Retrieves an entity identified by its primary key.
/// </summary>
/// <param name="id">Primary key value of the entity to retrieve.</param>
/// <returns>The matching entity, or null if no entity with the specified key exists.</returns>
Task<TEntity?> GetByIdAsync(TId id);

    /// <summary>
/// Retrieves all entities of type <typeparamref name="TEntity"/>.
/// </summary>
/// <returns>An enumerable containing every <typeparamref name="TEntity"/> in the repository.</returns>
Task<IEnumerable<TEntity>> GetAllAsync();

    /// <summary>
/// Inserts the provided entity into the repository and returns its primary key value.
/// </summary>
/// <param name="entity">The entity to insert.</param>
/// <returns>The primary key value (`TId`) of the inserted entity.</returns>
Task<TId> InsertAsync(TEntity entity);

    /// <summary>
/// Updates an existing entity in the repository.
/// </summary>
/// <param name="entity">The entity with updated values to persist.</param>
/// <returns>`true` if the update succeeded, `false` otherwise.</returns>
Task<bool> UpdateAsync(TEntity entity);

    /// <summary>
/// Deletes the entity with the specified primary key.
/// </summary>
/// <param name="id">The primary key of the entity to delete.</param>
/// <returns>`true` if the entity was deleted, `false` otherwise.</returns>
Task<bool> DeleteAsync(TId id);
}

/// <summary>
/// Skrót dla encji z kluczem int (zachowanie wstecznej kompatybilności).
/// </summary>
public interface IRepository<TEntity> : IRepository<TEntity, int>
    where TEntity : BaseEntity<int>
{
}

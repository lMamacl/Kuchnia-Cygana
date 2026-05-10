using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
{
    Task<TEntity?> GetByIdAsync(TId id);

    Task<IEnumerable<TEntity>> GetAllAsync();

    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);

    Task<TId> InsertAsync(TEntity entity);

    Task<bool> UpdateAsync(TEntity entity);

    Task<bool> DeleteAsync(TId id);
}

public interface IRepository<TEntity> : IRepository<TEntity, int>
    where TEntity : BaseEntity<int>
{
}

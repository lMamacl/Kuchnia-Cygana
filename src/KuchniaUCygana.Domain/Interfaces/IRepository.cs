using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IRepository<T>
    where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);

    Task<IEnumerable<T>> GetAllAsync();

    Task<int> InsertAsync(T entity);

    Task<bool> UpdateAsync(T entity);

    Task<bool> DeleteAsync(int id);
}

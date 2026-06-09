using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface ICategoryService
{
    Task<IEnumerable<Category>> GetAllAsync();

    Task<Category?> GetAsync(int id);

    Task<Category> CreateAsync(Category category);

    Task UpdateAsync(Category category);

    Task DeleteAsync(int id);
}

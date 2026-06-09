using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        this.categoryRepository = categoryRepository;
    }

    public async Task<IEnumerable<Category>> GetAllAsync() =>
        await this.categoryRepository.GetOrderedAsync();

    public async Task<Category?> GetAsync(int id) =>
        await this.categoryRepository.GetByIdAsync(id);

    public async Task<Category> CreateAsync(Category category)
    {
        await this.categoryRepository.InsertAsync(category);
        return category;
    }

    public async Task UpdateAsync(Category category) =>
        await this.categoryRepository.UpdateAsync(category);

    public async Task DeleteAsync(int id) =>
        await this.categoryRepository.DeleteAsync(id);
}

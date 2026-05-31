using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IRecipeComponentManagementService
{
    Task<IReadOnlyList<RecipeComponentListItemDto>> SearchAsync(string? query);

    Task<IReadOnlyList<RecipeComponentVersionOptionDto>> GetPublishedVersionOptionsAsync();

    Task<RecipeComponentDetailDto?> GetComponentAsync(int componentId);

    Task<RecipeComponentVersionDetailDto?> GetVersionAsync(int versionId);

    Task<int> CreateComponentAsync(CreateRecipeComponentRequest request);

    Task<int> CreateVersionAsync(CreateRecipeComponentVersionRequest request);

    Task UpdateVersionAsync(int versionId, UpdateRecipeComponentVersionRequest request);

    Task SaveIngredientAsync(SaveComponentIngredientRequest request);

    Task DeleteIngredientAsync(int ingredientId);

    Task SavePackagingAsync(SavePackagingRequirementRequest request);

    Task DeletePackagingAsync(int packagingRequirementId);

    Task PublishVersionAsync(int versionId);

    Task AttachComponentToMealAsync(AttachComponentToMealRequest request);
}

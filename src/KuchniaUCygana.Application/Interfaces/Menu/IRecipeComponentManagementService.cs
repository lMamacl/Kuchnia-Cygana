using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IRecipeComponentManagementService
{
    Task<PagedResultDto<RecipeComponentListItemDto>> SearchAsync(RecipeComponentSearchFilterDto filter);

    Task<IReadOnlyList<RecipeComponentVersionOptionDto>> GetPublishedVersionOptionsAsync();

    Task<IReadOnlyList<RecipeComponentVersionOptionDto>> SearchPublishedVersionOptionsAsync(string? query, int limit = 20);

    Task<RecipeComponentDetailDto?> GetComponentAsync(int componentId);

    Task<RecipeComponentVersionDetailDto?> GetVersionAsync(int versionId);

    Task<int> CreateComponentAsync(CreateRecipeComponentRequest request);

    Task<int> CreateVersionAsync(CreateRecipeComponentVersionRequest request);

    Task UpdateVersionAsync(int versionId, UpdateRecipeComponentVersionRequest request);

    Task SaveIngredientAsync(SaveComponentIngredientRequest request);

    Task DeleteIngredientAsync(int ingredientId);

    Task SavePackagingAsync(SavePackagingRequirementRequest request);

    Task DeletePackagingAsync(int packagingRequirementId);

    Task SaveInstructionSectionAsync(SaveInstructionSectionRequest request);

    Task SaveInstructionStepAsync(SaveInstructionStepRequest request);

    Task DeleteInstructionSectionAsync(int sectionId);

    Task DeleteInstructionStepAsync(int stepId);

    Task PublishVersionAsync(int versionId);

    Task AttachComponentToMealAsync(AttachComponentToMealRequest request);
}

using KuchniaUCygana.Application.DTOs.Production;

namespace KuchniaUCygana.Application.Interfaces;

public interface ICookingSessionService
{
    Task<CookingComponentSessionDto> GetComponentSessionAsync(int productionPlanItemId, int recipeComponentVersionId);

    Task<CookingComponentSessionDto> StartComponentSessionAsync(
        int productionPlanItemId,
        int recipeComponentVersionId,
        string operatorName);

    Task ToggleStepAsync(ToggleCookingStepRequest request);

    Task<CookingComponentSessionDto> CompleteComponentSessionAsync(
        int productionPlanItemId,
        int recipeComponentVersionId,
        string operatorName);

    Task StartComponentSessionAsync(int productionPlanItemId);

    Task ApproveComponentCookingAsync(int productionPlanItemId, decimal acceptedQuantity, string approvedBy);
}

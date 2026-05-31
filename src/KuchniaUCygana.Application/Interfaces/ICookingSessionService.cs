namespace KuchniaUCygana.Application.Interfaces;

public interface ICookingSessionService
{
    Task StartComponentSessionAsync(int productionPlanItemId);

    Task ApproveComponentCookingAsync(int productionPlanItemId, decimal acceptedQuantity, string approvedBy);
}

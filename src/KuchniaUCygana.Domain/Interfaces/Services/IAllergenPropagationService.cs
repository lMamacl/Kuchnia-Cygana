namespace KuchniaUCygana.Domain.Interfaces.Services.Menu;

public interface IAllergenPropagationService
{
    Task PropagateAllergenAsync(int mealId);
}

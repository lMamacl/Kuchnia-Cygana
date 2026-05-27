namespace KuchniaUCygana.Domain.Interfaces.Services.Menu;

public interface IIngredientDeletionGuard
{
    Task<bool> CanDeleteAsync(int ingredientId);
}

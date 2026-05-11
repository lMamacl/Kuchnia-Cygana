namespace KuchniaUCygana.Domain.Interfaces.Services.Menu;

public interface IDietVariantScaler
{
    Task RecalculateVariantAsync(int dietVariantId);

    decimal ScaleMealWeight(decimal baseWeightGrams, decimal multiplier);
}

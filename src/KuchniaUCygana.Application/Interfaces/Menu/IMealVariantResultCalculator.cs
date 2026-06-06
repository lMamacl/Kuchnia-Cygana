using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IMealVariantResultCalculator
{
    MealVariantResultDto Calculate(MealVariantResultCalculationRequest request);
}

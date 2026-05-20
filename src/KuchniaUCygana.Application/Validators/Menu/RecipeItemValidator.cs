using FluentValidation;
using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Validators.Menu;

public sealed class RecipeItemValidator : AbstractValidator<RecipeItemDto>
{
    public RecipeItemValidator()
    {
        this.RuleFor(x => x.IngredientId)
            .GreaterThan(0);
        this.RuleFor(x => x.WeightInGrams)
            .GreaterThan(0).WithMessage("Gramatura musi być większa od 0.");
    }
}

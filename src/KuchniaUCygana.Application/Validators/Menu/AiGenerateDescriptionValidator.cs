using FluentValidation;
using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Validators.Menu;

public sealed class AiGenerateDescriptionValidator : AbstractValidator<AiGenerateDescriptionRequest>
{
    public AiGenerateDescriptionValidator()
    {
        this.RuleFor(x => x.MealId)
            .GreaterThan(0);
    }
}

using FluentValidation;
using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Validators.Menu;

public sealed class CreateMealValidator : AbstractValidator<CreateMealRequest>
{
    public CreateMealValidator()
    {
        this.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa posiłku jest wymagana.")
            .MaximumLength(200);
        this.RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Należy wybrać kategorię.");
    }
}

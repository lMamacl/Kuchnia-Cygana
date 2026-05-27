using FluentValidation;
using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Validators.Menu;

public sealed class CreateDietValidator : AbstractValidator<CreateDietRequest>
{
    public CreateDietValidator()
    {
        this.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa diety jest wymagana.")
            .MaximumLength(200);
        this.RuleFor(x => x.Variants)
            .NotEmpty().WithMessage("Dieta musi mieć co najmniej jeden wariant kaloryczny.");
        this.RuleForEach(x => x.Variants).SetValidator(new CreateDietVariantValidator());
    }
}

internal sealed class CreateDietVariantValidator : AbstractValidator<CreateDietVariantRequest>
{
    public CreateDietVariantValidator()
    {
        this.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa wariantu jest wymagana.");
        this.RuleFor(x => x.TargetCalories)
            .GreaterThan(0).WithMessage("Kaloryczność musi być większa od 0.");
    }
}

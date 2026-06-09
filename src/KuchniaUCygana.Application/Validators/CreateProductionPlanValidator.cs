using FluentValidation;
using KuchniaUCygana.Application.DTOs.Production;

namespace KuchniaUCygana.Application.Validators;

public sealed class CreateProductionPlanValidator : AbstractValidator<CreateProductionPlanRequest>
{
    public CreateProductionPlanValidator()
    {
        RuleFor(x => x.ProductionDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Data produkcji nie może być w przeszłości.");

        RuleFor(x => x.Notes).MaximumLength(500)
            .When(x => x.Notes != null);
    }
}

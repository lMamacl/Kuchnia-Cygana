using FluentValidation;
using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Validators;

public sealed class RegisterWasteValidator : AbstractValidator<RegisterWasteRequest>
{
    public RegisterWasteValidator()
    {
        RuleFor(x => x.StockItemId).GreaterThan(0)
            .WithMessage("Składnik magazynowy jest wymagany.");

        RuleFor(x => x.Quantity).GreaterThan(0)
            .WithMessage("Ilość odpisu musi być większa od 0.");

        RuleFor(x => x.Reason).NotEmpty()
            .WithMessage("Powód odpisu jest wymagany.")
            .MaximumLength(250);

        RuleFor(x => x.BatchId).GreaterThan(0)
            .When(x => x.BatchId.HasValue)
            .WithMessage("ID partii musi być prawidłowe.");
    }
}

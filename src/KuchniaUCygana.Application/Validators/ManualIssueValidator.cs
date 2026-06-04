using FluentValidation;
using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Validators;

public sealed class ManualIssueValidator : AbstractValidator<ManualIssueRequest>
{
    public ManualIssueValidator()
    {
        RuleFor(x => x.StockItemId)
            .GreaterThan(0)
            .WithMessage("ID składnika musi być większe od 0.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Ilość musi być większa od 0.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Powód odpisu ręcznego jest wymagany.")
            .MaximumLength(250)
            .WithMessage("Powód nie może przekraczać 250 znaków.");

        RuleFor(x => x.IssuedTo)
            .NotEmpty()
            .WithMessage("Pola 'Osoba odbierająca' (IssuedTo) jest wymagane.")
            .MaximumLength(100)
            .WithMessage("Nazwa odbiorcy nie może przekraczać 100 znaków.");
    }
}

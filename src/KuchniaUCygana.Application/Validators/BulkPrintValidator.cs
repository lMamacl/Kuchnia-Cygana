using FluentValidation;
using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Application.Validators;

public sealed class BulkPrintValidator : AbstractValidator<BulkPrintRequest>
{
    public BulkPrintValidator()
    {
        RuleFor(x => x.SessionIds)
            .NotEmpty()
            .WithMessage("Lista identyfikatorów sesji nie może być pusta.")
            .Must(list => list != null && list.Count <= 50)
            .WithMessage("Maksymalna liczba sesji do jednorazowego druku to 50.");

        RuleFor(x => x.OperatorName)
            .NotEmpty()
            .WithMessage("Nazwa operatora jest wymagana.");
    }
}

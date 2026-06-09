using System;
using FluentValidation;
using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Validators;

public sealed class EditBatchExpiryValidator : AbstractValidator<EditBatchExpiryRequest>
{
    public EditBatchExpiryValidator()
    {
        RuleFor(x => x.BatchId)
            .GreaterThan(0)
            .WithMessage("ID partii musi być większe od 0.");

        RuleFor(x => x.NewExpiryDate)
            .NotEmpty()
            .WithMessage("Nowa data ważności jest wymagana.")
            .Must(date => date.Date >= DateTimeOffset.UtcNow.Date)
            .WithMessage("Nowa data ważności nie może być z przeszłości.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Powód zmiany jest wymagany.")
            .MinimumLength(5)
            .WithMessage("Powód zmiany musi mieć co najmniej 5 znaków.")
            .MaximumLength(250)
            .WithMessage("Powód zmiany nie może przekraczać 250 znaków.");
    }
}

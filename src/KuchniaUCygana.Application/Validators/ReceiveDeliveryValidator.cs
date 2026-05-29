using FluentValidation;
using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Validators;

public sealed class ReceiveDeliveryValidator : AbstractValidator<ReceiveDeliveryRequest>
{
    public ReceiveDeliveryValidator()
    {
        RuleFor(x => x.StockItemId).GreaterThan(0)
            .WithMessage("Składnik magazynowy jest wymagany.");

        RuleFor(x => x.SupplierBatchNumber).NotEmpty()
            .WithMessage("Numer partii dostawcy jest wymagany.")
            .MaximumLength(50);

        RuleFor(x => x.Quantity).GreaterThan(0)
            .WithMessage("Ilość musi być większa od 0.");

        RuleFor(x => x.ExpiryDate)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(x => x.ExpiryDate.HasValue)
            .WithMessage("Data ważności musi być w przyszłości.");

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(50)
            .WithMessage("Numer faktury/dostawy może mieć maksymalnie 50 znaków.");
    }
}

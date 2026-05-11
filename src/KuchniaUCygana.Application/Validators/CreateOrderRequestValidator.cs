using FluentValidation;
using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.AddressId).GreaterThan(0);
        RuleFor(x => x.StartDate).GreaterThanOrEqualTo(DateTime.Today.AddDays(1))
            .WithMessage("Data rozpoczęcia musi być co najmniej jutro.");
        RuleFor(x => x.Items).NotEmpty()
            .WithMessage("Zamówienie musi zawierać co najmniej jedną dietę.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.TotalDays).InclusiveBetween(1, 365);
            item.RuleFor(i => i.PricePerDay).GreaterThan(0);
        });
    }
}

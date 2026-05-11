using FluentValidation;
using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Validators;

public sealed class ApplyDiscountRequestValidator : AbstractValidator<ApplyDiscountRequest>
{
    public ApplyDiscountRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}

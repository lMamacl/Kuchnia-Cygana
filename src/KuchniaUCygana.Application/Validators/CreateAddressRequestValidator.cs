using FluentValidation;
using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Validators;

public sealed class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    public CreateAddressRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Street).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BuildingNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode)
            .NotEmpty()
            .Matches(@"^\d{2}-\d{3}$")
            .WithMessage("Kod pocztowy musi być w formacie XX-XXX.");
    }
}

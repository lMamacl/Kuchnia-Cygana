using FluentValidation;
using KuchniaUCygana.Application.DTOs.HR;

namespace KuchniaUCygana.Application.Validators.HR;

public sealed class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa dzialu jest wymagana.")
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(200)
            .When(x => x.Description is not null);

        RuleFor(x => x.HeadEmployeeId)
            .GreaterThan(0)
            .When(x => x.HeadEmployeeId.HasValue);
    }
}

public sealed class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa dzialu jest wymagana.")
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(200)
            .When(x => x.Description is not null);

        RuleFor(x => x.HeadEmployeeId)
            .GreaterThan(0)
            .When(x => x.HeadEmployeeId.HasValue);
    }
}

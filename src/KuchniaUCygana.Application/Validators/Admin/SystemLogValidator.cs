using FluentValidation;
using KuchniaUCygana.Application.DTOs.Admin;

namespace KuchniaUCygana.Application.Validators.Admin;

public sealed class CreateSystemLogRequestValidator : AbstractValidator<CreateSystemLogRequest>
{
    public CreateSystemLogRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Akcja audytu jest wymagana.")
            .MaximumLength(100);

        RuleFor(x => x.TargetEntity)
            .NotEmpty().WithMessage("Encja docelowa jest wymagana.")
            .MaximumLength(50);

        RuleFor(x => x.TargetId)
            .NotEmpty().WithMessage("Identyfikator obiektu jest wymagany.")
            .MaximumLength(100);

        RuleFor(x => x.IPAddress)
            .MaximumLength(45)
            .When(x => x.IPAddress is not null);
    }
}

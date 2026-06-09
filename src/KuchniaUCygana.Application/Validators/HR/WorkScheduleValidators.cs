using FluentValidation;
using KuchniaUCygana.Application.DTOs.HR;

namespace KuchniaUCygana.Application.Validators.HR;

public sealed class CreateWorkScheduleRequestValidator : AbstractValidator<CreateWorkScheduleRequest>
{
    public CreateWorkScheduleRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);

        RuleFor(x => x.ShiftDate)
            .NotEqual(default(DateOnly)).WithMessage("Data zmiany jest wymagana.");

        RuleFor(x => x.Shift)
            .IsInEnum().WithMessage("Wybrana zmiana jest niepoprawna.");

        RuleFor(x => x.RoleAtShift)
            .MaximumLength(50)
            .When(x => x.RoleAtShift is not null);
    }
}

public sealed class UpdateWorkScheduleRequestValidator : AbstractValidator<UpdateWorkScheduleRequest>
{
    public UpdateWorkScheduleRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);

        RuleFor(x => x.ShiftDate)
            .NotEqual(default(DateOnly)).WithMessage("Data zmiany jest wymagana.");

        RuleFor(x => x.Shift)
            .IsInEnum().WithMessage("Wybrana zmiana jest niepoprawna.");

        RuleFor(x => x.RoleAtShift)
            .MaximumLength(50)
            .When(x => x.RoleAtShift is not null);
    }
}

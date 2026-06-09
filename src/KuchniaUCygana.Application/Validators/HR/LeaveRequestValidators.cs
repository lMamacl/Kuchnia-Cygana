using FluentValidation;
using KuchniaUCygana.Application.DTOs.HR;

namespace KuchniaUCygana.Application.Validators.HR;

public sealed class CreateLeaveRequestRequestValidator : AbstractValidator<CreateLeaveRequestRequest>
{
    public CreateLeaveRequestRequestValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);

        RuleFor(x => x.LeaveType)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Typ urlopu jest niepoprawny.");

        RuleFor(x => x.StartDate)
            .NotEqual(default(DateOnly)).WithMessage("Data poczatku urlopu jest wymagana.")
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Urlop nie moze zaczynac sie w przeszlosci.");

        RuleFor(x => x.EndDate)
            .NotEqual(default(DateOnly)).WithMessage("Data konca urlopu jest wymagana.")
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Data konca urlopu nie moze byc wczesniejsza niz data poczatku.");
    }
}

public sealed class UpdateLeaveRequestRequestValidator : AbstractValidator<UpdateLeaveRequestRequest>
{
    public UpdateLeaveRequestRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.LeaveType)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Typ urlopu jest niepoprawny.");

        RuleFor(x => x.StartDate)
            .NotEqual(default(DateOnly)).WithMessage("Data poczatku urlopu jest wymagana.");

        RuleFor(x => x.EndDate)
            .NotEqual(default(DateOnly)).WithMessage("Data konca urlopu jest wymagana.")
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Data konca urlopu nie moze byc wczesniejsza niz data poczatku.");
    }
}

public sealed class ReviewLeaveRequestRequestValidator : AbstractValidator<ReviewLeaveRequestRequest>
{
    public ReviewLeaveRequestRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Status)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Status wniosku urlopowego jest niepoprawny.");

        RuleFor(x => x.ApprovedByEmployeeId)
            .GreaterThan(0)
            .When(x => x.ApprovedByEmployeeId.HasValue);

        RuleFor(x => x.RejectionReason)
            .MaximumLength(500)
            .When(x => x.RejectionReason is not null);
    }
}

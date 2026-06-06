using FluentValidation;
using KuchniaUCygana.Application.DTOs.HR;

namespace KuchniaUCygana.Application.Validators.HR;

public sealed class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        AddEmployeeRules();
    }

    private void AddEmployeeRules()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Imie pracownika jest wymagane.")
            .MaximumLength(50);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Nazwisko pracownika jest wymagane.")
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email pracownika jest wymagany.")
            .EmailAddress()
            .MaximumLength(100);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(15)
            .Matches(@"^\+?[0-9\s-]{7,15}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Numer telefonu ma niepoprawny format.");

        RuleFor(x => x.HireDate)
            .NotEqual(default(DateOnly)).WithMessage("Data zatrudnienia jest wymagana.");

        RuleFor(x => x)
            .Must(x => !x.TerminationDate.HasValue || x.TerminationDate.Value >= x.HireDate)
            .WithMessage("Data zakonczenia pracy nie moze byc wczesniejsza niz data zatrudnienia.");

        RuleFor(x => x.DepartmentId).GreaterThan(0);

        RuleFor(x => x.Position)
            .NotEmpty().WithMessage("Stanowisko jest wymagane.")
            .MaximumLength(100);
    }
}

public sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Imie pracownika jest wymagane.")
            .MaximumLength(50);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Nazwisko pracownika jest wymagane.")
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email pracownika jest wymagany.")
            .EmailAddress()
            .MaximumLength(100);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(15)
            .Matches(@"^\+?[0-9\s-]{7,15}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Numer telefonu ma niepoprawny format.");

        RuleFor(x => x.HireDate)
            .NotEqual(default(DateOnly)).WithMessage("Data zatrudnienia jest wymagana.");

        RuleFor(x => x)
            .Must(x => !x.TerminationDate.HasValue || x.TerminationDate.Value >= x.HireDate)
            .WithMessage("Data zakonczenia pracy nie moze byc wczesniejsza niz data zatrudnienia.");

        RuleFor(x => x.DepartmentId).GreaterThan(0);

        RuleFor(x => x.Position)
            .NotEmpty().WithMessage("Stanowisko jest wymagane.")
            .MaximumLength(100);
    }
}

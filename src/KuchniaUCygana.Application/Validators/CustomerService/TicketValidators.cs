using FluentValidation;
using KuchniaUCygana.Application.DTOs.CustomerService;

namespace KuchniaUCygana.Application.Validators.CustomerService;

public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.ClientUserId).GreaterThan(0);

        RuleFor(x => x.OrderId)
            .GreaterThan(0)
            .When(x => x.OrderId.HasValue);

        RuleFor(x => x.DeliveryCalendarId)
            .GreaterThan(0)
            .When(x => x.DeliveryCalendarId.HasValue);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tytul zgloszenia jest wymagany.")
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Opis zgloszenia jest wymagany.");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Priorytet zgloszenia jest niepoprawny.");
    }
}

public sealed class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tytul zgloszenia jest wymagany.")
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Opis zgloszenia jest wymagany.");

        RuleFor(x => x.OrderId)
            .GreaterThan(0)
            .When(x => x.OrderId.HasValue);

        RuleFor(x => x.DeliveryCalendarId)
            .GreaterThan(0)
            .When(x => x.DeliveryCalendarId.HasValue);

        RuleFor(x => x.AssignedToUserId)
            .GreaterThan(0)
            .When(x => x.AssignedToUserId.HasValue);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status zgloszenia jest niepoprawny.");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Priorytet zgloszenia jest niepoprawny.");
    }
}

public sealed class AssignTicketRequestValidator : AbstractValidator<AssignTicketRequest>
{
    public AssignTicketRequestValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);
        RuleFor(x => x.AssignedToUserId).GreaterThan(0);
    }
}

public sealed class ChangeTicketStatusRequestValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    public ChangeTicketStatusRequestValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status zgloszenia jest niepoprawny.");
    }
}

using FluentValidation;
using KuchniaUCygana.Application.DTOs.CustomerService;

namespace KuchniaUCygana.Application.Validators.CustomerService;

public sealed class CreateTicketAttachmentRequestValidator : AbstractValidator<CreateTicketAttachmentRequest>
{
    public CreateTicketAttachmentRequestValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Nazwa pliku jest wymagana.")
            .MaximumLength(255);

        RuleFor(x => x.FilePath)
            .NotEmpty().WithMessage("Sciezka pliku jest wymagana.")
            .MaximumLength(500);

        RuleFor(x => x.UploadedByUserId).GreaterThan(0);
    }
}

using FluentValidation;
using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Validators.Menu;

public sealed class UploadImageValidator : AbstractValidator<UploadImageRequest>
{
    public UploadImageValidator()
    {
        this.RuleFor(x => x.MealId)
            .GreaterThan(0);
    }
}

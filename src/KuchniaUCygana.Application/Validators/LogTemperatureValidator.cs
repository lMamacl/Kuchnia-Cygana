using FluentValidation;
using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Validators;

public sealed class LogTemperatureValidator : AbstractValidator<LogTemperatureRequest>
{
    public LogTemperatureValidator()
    {
        RuleFor(x => x.DeviceNameOrLocation)
            .NotEmpty()
            .When(x => !x.HaccpLocationId.HasValue)
            .WithMessage("Lokalizacja/urządzenie jest wymagane.");

        RuleFor(x => x.DeviceNameOrLocation)
            .MaximumLength(120)
            .WithMessage("Nazwa lokalizacji może mieć maksymalnie 120 znaków.");

        RuleFor(x => x.TemperatureCelsius)
            .InclusiveBetween(-50m, 50m)
            .WithMessage("Temperatura musi być w zakresie -50°C do +50°C (weryfikacja sensora).");
    }
}

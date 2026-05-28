using FluentValidation;
using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Validators;

public sealed class LogTemperatureValidator : AbstractValidator<LogTemperatureRequest>
{
    /// <summary>
    /// Zakres HACCP: od -25°C (mroźnie) do +8°C (chłodnie).
    /// Odczyty poza zakresem są rejestrowane, ale generują alert.
    /// Walidator przepuszcza zakres -50°C do +50°C (zabezpieczenie przed błędami sensorów).
    /// </summary>
    public LogTemperatureValidator()
    {
        RuleFor(x => x.DeviceNameOrLocation).NotEmpty()
            .WithMessage("Lokalizacja/urządzenie jest wymagane.")
            .MaximumLength(50);

        RuleFor(x => x.TemperatureCelsius)
            .InclusiveBetween(-50m, 50m)
            .WithMessage("Temperatura musi być w zakresie -50°C do +50°C (weryfikacja sensora).");
    }
}

using System.ComponentModel.DataAnnotations;

namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class CreateDriverRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Wybierz konto użytkownika.")]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Podaj numer prawa jazdy.")]
    [StringLength(50, ErrorMessage = "Numer prawa jazdy może mieć maksymalnie 50 znaków.")]
    public string LicenseNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

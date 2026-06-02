/*
 * Plik: Entities/Admin/SystemLog.cs
 * Opis: Encja rejestrująca audyt zmian w systemie – kto, kiedy, co zmienił (stare i nowe wartości).
 *       Powiązana z użytkownikiem. Przechowuje również adres IP.
 */

using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Admin;


public sealed class SystemLog : BaseEntity
{
    public int UserId { get; set; }
    
    public string Action { get; set; } = string.Empty;
    public string TargetEntity { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? IPAddress { get; set; }
}

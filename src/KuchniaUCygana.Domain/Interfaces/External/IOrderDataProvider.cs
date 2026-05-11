using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KuchniaUCygana.Domain.Interfaces.External;

/// <summary>
/// Dane aktywnego zamówienia z Modułu 1. Używane do generowania planu produkcji.
/// </summary>
public sealed class ActiveOrderEntry
{
    public int OrderId { get; set; }

    public int ClientId { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public int DietVariantId { get; set; }

    public DateOnly DeliveryDate { get; set; }
}

/// <summary>
/// Dostawca danych zamówień z Modułu 1.
/// Mock w fazie rozwoju M3 → adapter do prawdziwego M1 w fazie integracji.
/// </summary>
public interface IOrderDataProvider
{
    /// <summary>
    /// Pobiera aktywne zamówienia na konkretny dzień dostawy.
    /// </summary>
    Task<IEnumerable<ActiveOrderEntry>> GetActiveOrdersAsync(DateOnly deliveryDate);

    /// <summary>
    /// Pobiera zamówienie po ID.
    /// </summary>
    Task<ActiveOrderEntry?> GetOrderByIdAsync(int orderId);
}

namespace KuchniaUCygana.Domain.Interfaces.External;

// Kontrakt między Modułem 1 a Modułem 3.
// M3 używa tego interfejsu do pobrania aktywnych zamówień na dany dzień

public interface IOrderDataProvider
{
    // Zwraca listę dostaw zaplanowanych na podany dzień wraz z adresami,
    // oknami czasowymi i pozycjami zamówień (diety + warianty).
    Task<IEnumerable<OrderDeliveryInfo>> GetDeliveriesForDateAsync(DateTime date);
}

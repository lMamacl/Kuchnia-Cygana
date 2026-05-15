using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces;

/// <summary>
/// Definicja operacji bazodanowych na torbach
/// </summary>

public interface IThermalBagRepository : IRepository<ThermalBag>
{
    Task<ThermalBag?> GetBySerialNumberAsync(string serialNumber);
}
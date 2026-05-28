using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Definicja operacji bazodanowych na kierowcach
/// </summary>

public interface IDriverRepository : IRepository<Driver>
{
    //jeszcze nie wiem
    //Task<Driver?> GetByEmailAsync(string email);
}
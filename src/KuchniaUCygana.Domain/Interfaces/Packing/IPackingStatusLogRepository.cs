using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Packing;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingStatusLogRepository : IRepository<PackingStatusLog>
{
    /// <summary>
    /// Pobiera wszystkie logi zmian statusu dla danej sesji pakowania,
    /// posortowane malejąco po dacie zmiany.
    /// </summary>
    Task<IEnumerable<PackingStatusLog>> GetBySessionIdAsync(int packingSessionId);
}

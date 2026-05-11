using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Packing;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingSessionRepository : IRepository<PackingSession>
{
    /// <summary>
    /// Pobiera aktywne sesje pakowania na konkretną datę.
    /// </summary>
    Task<IEnumerable<PackingSession>> GetActiveByDateAsync(DateOnly date);

    /// <summary>
    /// Pobiera sesję z załadowanymi pozycjami (eager loading).
    /// </summary>
    Task<PackingSession?> GetWithItemsAsync(int sessionId);

    /// <summary>
    /// Pobiera pozycje sesji pakowania.
    /// </summary>
    Task<IEnumerable<PackingItem>> GetSessionItemsAsync(int sessionId);
}

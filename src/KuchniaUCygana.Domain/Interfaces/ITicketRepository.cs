/*
 * Plik: Interfaces/ITicketRepository.cs
 * Opis: Kontrakt dla repozytorium ticketów – rozszerza IRepository o specyficzne zapytania: 
 *       GetByClientIdAsync, GetByStatusAsync, GetOpenTicketsAsync.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces;

public interface ITicketRepository : IRepository<Ticket>
{
    Task<IEnumerable<Ticket>> GetByClientIdAsync(int clientUserId);
    Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status);
    Task<IEnumerable<Ticket>> GetOpenTicketsAsync();
}

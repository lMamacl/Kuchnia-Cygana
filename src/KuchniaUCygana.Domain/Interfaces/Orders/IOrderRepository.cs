using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces.Orders;

public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId);
    Task<(IReadOnlyList<CustomerOrderSearchRow> Items, int TotalCount)> SearchByCustomerAsync(CustomerOrderSearchQuery query);
    Task<Order?> GetWithItemsAndDeliveryAsync(int orderId);
    Task<IReadOnlyDictionary<int, Order>> GetWithItemsAndDeliveryByIdsAsync(IEnumerable<int> orderIds);
    Task<IEnumerable<Order>> GetActiveOrdersForDateAsync(DateTime date);
    Task<Order?> GetByOrderNumberAsync(string orderNumber);
    Task<string> GenerateOrderNumberAsync();
    Task<int> InsertCheckoutAsync(
        Order order,
        IReadOnlyCollection<OrderItem> items,
        IReadOnlyCollection<DeliveryCalendar> deliveryDays);
}

public sealed class CustomerOrderSearchQuery
{
    public int CustomerId { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public OrderStatus? Status { get; init; }

    public DateTime? DateFrom { get; init; }

    public DateTime? DateTo { get; init; }

    public string? OrderNumber { get; init; }
}

public sealed class CustomerOrderSearchRow
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public decimal FinalPrice { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int ItemCount { get; set; }
}

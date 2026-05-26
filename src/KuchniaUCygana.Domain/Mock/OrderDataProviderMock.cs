using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Domain.Mocks;

public sealed class OrderDataProviderMock : IOrderDataProvider
{
    public Task<object?> GetOrderByIdAsync(int orderId)
        => Task.FromResult<object?>(new { Id = orderId, Status = "Mock", Total = 0m });

    public Task<IEnumerable<object>> GetOrdersByClientIdAsync(int clientUserId)
        => Task.FromResult(Enumerable.Empty<object>());
}
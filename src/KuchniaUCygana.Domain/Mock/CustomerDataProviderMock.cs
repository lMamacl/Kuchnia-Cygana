using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Domain.Mocks;

public sealed class CustomerDataProviderMock : ICustomerDataProvider
{
    public Task<object?> GetCustomerByIdAsync(int userId)
        => Task.FromResult<object?>(new { Id = userId, Email = "mock@example.com", FullName = "Mock Customer" });
}
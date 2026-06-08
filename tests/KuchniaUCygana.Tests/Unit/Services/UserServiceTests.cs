using FluentAssertions;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class UserServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsPagedMappedDtos()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.SearchAsync(It.IsAny<UserSearchQuery>())).ReturnsAsync(new UserSearchResult
        {
            Items = new List<User>
            {
                new() { Id = 1, Email = "a@b.pl", FirstName = "Jan", LastName = "Kowalski", Role = "Client" },
            },
            Page = 1,
            PageSize = 20,
            TotalCount = 1,
        });

        IUserService service = new UserService(repository.Object);

        var result = await service.SearchAsync("Client", "Jan", 1, 20);

        result.Items.Should().ContainSingle(x => x.Email == "a@b.pl" && x.FullName == "Jan Kowalski");
        result.TotalCount.Should().Be(1);
        repository.Verify(r => r.SearchAsync(It.Is<UserSearchQuery>(query =>
            query.Role == "Client" &&
            query.Search == "Jan" &&
            query.Page == 1 &&
            query.PageSize == 20)), Times.Once);
    }
}

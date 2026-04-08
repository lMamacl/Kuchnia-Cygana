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
    public async Task GetAllAsync_ReturnsMappedDtos()
    {
        var repository = new Mock<IRepository<User>>();
        repository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>
        {
            new() { Id = 1, Email = "a@b.pl", FirstName = "Jan", LastName = "Kowalski", Role = "Client" },
        });

        IUserService service = new UserService(repository.Object);

        var result = await service.GetAllAsync();

        result.Should().ContainSingle(x => x.Email == "a@b.pl" && x.FullName == "Jan Kowalski");
    }
}

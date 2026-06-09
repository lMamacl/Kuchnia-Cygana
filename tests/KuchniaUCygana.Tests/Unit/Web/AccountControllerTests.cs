using System.Security.Claims;
using FluentAssertions;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Web.Controllers;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Web;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task DevLogin_ShouldRedirectToNormalLoginWithSeedEmail_WithoutSigningIn()
    {
        var authService = new Mock<IAuthenticationService>();
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.FindByEmailAsync("packing@kuchnia.local"))
            .ReturnsAsync(new User
            {
                Id = 10,
                Email = "packing@kuchnia.local",
                FirstName = "Pakowanie",
                LastName = "Pracownik",
                Role = UserRoles.Packing,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Packing123!"),
            });
        var controller = CreateController(userRepository, authService);

        var result = await controller.DevLogin(UserRoles.Packing);

        authService.Verify(service => service.SignInAsync(
            It.IsAny<HttpContext>(),
            CookieAuthenticationDefaults.AuthenticationScheme,
            It.IsAny<ClaimsPrincipal>(),
            It.IsAny<AuthenticationProperties?>()), Times.Never);
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Login");
        redirect.RouteValues.Should().ContainKey("email").WhoseValue.Should().Be("packing@kuchnia.local");
    }

    [Fact]
    public void Login_ShouldPrefillEmail_WhenDevelopmentShortcutProvidesEmail()
    {
        var controller = CreateController(new Mock<IUserRepository>(), new Mock<IAuthenticationService>());

        var result = controller.Login(email: "logistics@kuchnia.local");

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<LoginViewModel>().Subject;
        model.Email.Should().Be("logistics@kuchnia.local");
    }

    [Fact]
    public async Task Register_ShouldCreateClientAccount_WithHashedPassword()
    {
        User? insertedUser = null;
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.ExistsWithEmailAsync("nowy@kuchnia.local"))
            .ReturnsAsync(false);
        userRepository
            .Setup(repo => repo.InsertAsync(It.IsAny<User>()))
            .Callback<User>(user => insertedUser = user)
            .ReturnsAsync(42);
        var controller = CreateController(userRepository, new Mock<IAuthenticationService>());

        var result = await controller.Register(new RegisterViewModel
        {
            Email = " Nowy@Kuchnia.Local ",
            FirstName = "Jan",
            LastName = "Testowy",
            Password = "Sekret123!",
        });

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Login");
        insertedUser.Should().NotBeNull();
        insertedUser!.Email.Should().Be("nowy@kuchnia.local");
        insertedUser.FirstName.Should().Be("Jan");
        insertedUser.LastName.Should().Be("Testowy");
        insertedUser.Role.Should().Be(UserRoles.Client);
        insertedUser.PasswordHash.Should().NotBe("Sekret123!");
        BCrypt.Net.BCrypt.Verify("Sekret123!", insertedUser.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Register_ShouldReturnViewWithError_WhenEmailAlreadyExists()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.ExistsWithEmailAsync("demo@kuchnia.local"))
            .ReturnsAsync(true);
        var controller = CreateController(userRepository, new Mock<IAuthenticationService>());

        var result = await controller.Register(new RegisterViewModel
        {
            Email = "demo@kuchnia.local",
            FirstName = "Demo",
            LastName = "Klient",
            Password = "Demo123!",
        });

        result.Should().BeOfType<ViewResult>();
        controller.ModelState[nameof(RegisterViewModel.Email)]!.Errors.Should().NotBeEmpty();
        userRepository.Verify(repo => repo.InsertAsync(It.IsAny<User>()), Times.Never);
    }

    private static AccountController CreateController(
        Mock<IUserRepository> userRepository,
        Mock<IAuthenticationService> authService)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        var services = new ServiceCollection()
            .AddSingleton(authService.Object)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var controller = new AccountController(
            env.Object,
            userRepository.Object,
            Mock.Of<IHumanResourcesService>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
            Url = Mock.Of<IUrlHelper>(),
        };

        return controller;
    }
}

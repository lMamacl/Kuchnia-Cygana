using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KuchniaUCygana.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}

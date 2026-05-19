using FluentValidation;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services.Logistics;
using System.Reflection;

namespace KuchniaUCygana.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddMaps(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));
        
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<Services.Logistics.GeocodingOrchestrator>();
        services.AddScoped<IVehicleService, VehicleService>();
        return services;
    }
}

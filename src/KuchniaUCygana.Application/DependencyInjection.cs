using FluentValidation;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Application.Services.Menu;

namespace KuchniaUCygana.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddMaps(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));
        
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMealManagementService, MealManagementService>();
        services.AddScoped<IDietManagementService, DietManagementService>();
        services.AddScoped<IIngredientManagementService, IngredientManagementService>();
        services.AddScoped<IAllergenManagementService, AllergenManagementService>();
        services.AddScoped<IAiDescriptionService, AiDescriptionService>();
        services.AddScoped<IImageManagementService, ImageManagementService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<INutritionService, NutritionService>();
        return services;
    }
}

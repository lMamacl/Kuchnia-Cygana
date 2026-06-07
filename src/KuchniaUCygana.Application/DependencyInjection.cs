using FluentValidation;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace KuchniaUCygana.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddMaps(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IDeliveryCalendarService, DeliveryCalendarService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<ICustomerProfileService, CustomerProfileService>();
        services.AddScoped<IHumanResourcesService, HumanResourcesService>();
        services.AddScoped<ICustomerSupportService, CustomerSupportService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IStaffActivityService, StaffActivityService>();
        services.AddScoped<IStaffShiftAccessService, StaffShiftAccessService>();
        return services;
    }
}

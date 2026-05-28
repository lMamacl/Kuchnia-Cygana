using FluentValidation;
using FluentValidation.AspNetCore;
using KuchniaUCygana.Application;
using KuchniaUCygana.Infrastructure;
using KuchniaUCygana.Infrastructure.Persistence.Migrations;
using KuchniaUCygana.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using QuestPDF.Infrastructure;
using System.IO;

using Serilog;

var isSeedCommand = args.Any(x =>
    x.Equals("seed", StringComparison.OrdinalIgnoreCase) ||
    x.Equals("--seed", StringComparison.OrdinalIgnoreCase) ||
    x.Equals("db:seed", StringComparison.OrdinalIgnoreCase));

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDataProtection()
    .SetApplicationName("KuchniaUCygana")
    .PersistKeysToFileSystem(new DirectoryInfo("./Keys"));
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(
        options =>
        {
            options.Cookie.Name = "KuchniaUCygana.Auth";
            options.Cookie.HttpOnly = true;
            // Production should keep Always; local HTTP development can switch to SameAsRequest.
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.LogoutPath = "/Account/Logout";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api") ||
                    context.Request.Headers.XRequestedWith == "XMLHttpRequest")
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api") ||
                    context.Request.Headers.XRequestedWith == "XMLHttpRequest")
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<Program>());

var app = builder.Build();

app.UseSerilogRequestLogging();

QuestPDF.Settings.License = LicenseType.Community;
var skipDatabaseStartup = !isSeedCommand && app.Configuration.GetValue<bool>("DatabaseStartup:Skip");
if (skipDatabaseStartup)
{
    app.Logger.LogInformation("Database startup tasks skipped by configuration.");
}
else
{
    MigrationRunner.RunMigrations(app.Services, app.Logger);
    await DatabaseSeedingBootstrapper.TrySeedAsync(
        app.Services,
        app.Configuration,
        app.Environment,
        app.Logger,
        isSeedCommand ? SeedingTrigger.Command : SeedingTrigger.Startup);
}

if (isSeedCommand)
{
    app.Logger.LogInformation("Seed command completed. Exiting without starting web host.");
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

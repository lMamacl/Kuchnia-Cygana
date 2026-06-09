using KuchniaUCygana.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace KuchniaUCygana.Infrastructure.Configuration;

public sealed class ConfigurationApplicationUrlProvider : IApplicationUrlProvider
{
    private readonly IConfiguration configuration;

    public ConfigurationApplicationUrlProvider(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public string BaseUrl
    {
        get
        {
            var configured = configuration["Application:BaseUrl"];
            return string.IsNullOrWhiteSpace(configured)
                ? "http://localhost:5000"
                : configured.TrimEnd('/');
        }
    }
}

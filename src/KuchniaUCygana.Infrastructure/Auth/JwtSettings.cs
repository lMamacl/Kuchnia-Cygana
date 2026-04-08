namespace KuchniaUCygana.Infrastructure.Auth;

public sealed class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "KuchniaUCygana";

    public string Audience { get; set; } = "KuchniaUCyganaUsers";

    public int ExpiryHours { get; set; } = 8;
}

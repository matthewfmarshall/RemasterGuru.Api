namespace RemasterGuru.Api.Auth;

public static class Auth0Settings
{
    public const string SectionName = "Auth0";

    public static bool IsConfigured(IConfiguration configuration)
    {
        var domain = configuration[$"{SectionName}:Domain"];
        return !string.IsNullOrWhiteSpace(domain);
    }

    public static string GetAuthority(IConfiguration configuration)
    {
        var domain = configuration[$"{SectionName}:Domain"]?.Trim()
            ?? throw new InvalidOperationException("Auth0:Domain is not configured.");
        return domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? domain.TrimEnd('/')
            : $"https://{domain.TrimEnd('/')}";
    }
}

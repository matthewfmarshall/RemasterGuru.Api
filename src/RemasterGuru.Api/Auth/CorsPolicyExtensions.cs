namespace RemasterGuru.Api.Auth;

internal static class CorsPolicyExtensions
{
    public const string AppPolicyName = "RemasterGuruApp";

    public static void AddRemasterGuruCors(this IServiceCollection services, IConfiguration configuration)
    {
        var webBase = configuration["App:WebBaseUrl"]?.Trim();
        var extraOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var hfSpaceUrl = configuration["HuggingFace:SpaceUrl"]?.Trim();

        services.AddCors(options =>
        {
            options.AddPolicy(AppPolicyName, policy =>
            {
                policy
                    .SetIsOriginAllowed(origin =>
                    {
                        if (string.IsNullOrWhiteSpace(origin))
                        {
                            return false;
                        }

                        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        {
                            return false;
                        }

                        if (uri.Scheme is not ("http" or "https"))
                        {
                            return false;
                        }

                        if (!string.IsNullOrWhiteSpace(webBase)
                            && string.Equals(origin.TrimEnd('/'), webBase.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        foreach (var allowed in extraOrigins)
                        {
                            if (string.IsNullOrWhiteSpace(allowed))
                            {
                                continue;
                            }

                            if (string.Equals(origin.TrimEnd('/'), allowed.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(hfSpaceUrl)
                            && string.Equals(origin.TrimEnd('/'), hfSpaceUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        if (uri.Host.EndsWith(".hf.space", StringComparison.OrdinalIgnoreCase)
                            || uri.Host.Equals("hf.space", StringComparison.OrdinalIgnoreCase))
                        {
                            return configuration.GetValue("Cors:AllowHuggingFaceSpaceHosts", false);
                        }

                        return false;
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }
}

namespace RemasterGuru.Api.Auth;

internal static class ApiAuthPathRules
{
    public static bool IsAnonymousPath(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            && path.StartsWith("/api/v1/internal/upload/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && path.Equals("/api/v1/webhooks/stripe", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && path.Equals("/api/v1/webhooks/rpi", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

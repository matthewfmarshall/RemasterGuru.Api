using RemasterGuru.Infrastructure.Repositories;

namespace RemasterGuru.Api.Auth;

public sealed class DevUserAuthMiddleware(RequestDelegate next)
{
    public const string UserIdItemKey = "UserId";

    public async Task InvokeAsync(HttpContext context, IUserRepository users)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (IsAnonymousPath(context.Request))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-User-Id", out var headerValues)
            || !Guid.TryParse(headerValues.FirstOrDefault(), out var userId))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Missing or invalid X-User-Id header.");
            return;
        }

        await users.GetOrCreateAsync(userId, context.RequestAborted);
        context.Items[UserIdItemKey] = userId;
        await next(context);
    }

    private static bool IsAnonymousPath(HttpRequest request)
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

        return false;
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = status switch
            {
                StatusCodes.Status401Unauthorized => "Unauthorized",
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status404NotFound => "Not found",
                _ => "Error"
            },
            status,
            detail,
            traceId = context.TraceIdentifier
        });
    }
}

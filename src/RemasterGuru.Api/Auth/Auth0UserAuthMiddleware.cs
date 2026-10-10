using System.Security.Claims;
using RemasterGuru.Infrastructure.Repositories;

namespace RemasterGuru.Api.Auth;

public sealed class Auth0UserAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserRepository users)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (ApiAuthPathRules.IsAnonymousPath(context.Request))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Authentication required.");
            return;
        }

        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Token is missing the subject (sub) claim.");
            return;
        }

        var user = await users.GetOrCreateByAuth0SubjectAsync(subject, context.RequestAborted);
        context.Items[DevUserAuthMiddleware.UserIdItemKey] = user.Id;
        await next(context);
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

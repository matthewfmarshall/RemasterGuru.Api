namespace RemasterGuru.Api.Auth;

public interface ICurrentUser
{
    Guid UserId { get; }
}

public sealed class CurrentUser(Guid userId) : ICurrentUser
{
    public Guid UserId { get; } = userId;
}

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var context = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("No HTTP context.");
            if (context.Items.TryGetValue(DevUserAuthMiddleware.UserIdItemKey, out var value) && value is Guid id)
            {
                return id;
            }

            throw new InvalidOperationException("User is not authenticated.");
        }
    }
}

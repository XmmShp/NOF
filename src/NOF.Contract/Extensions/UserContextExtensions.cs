using System.Security.Authentication;

namespace NOF;

public static class UserContextExtensions
{
    public static string GetRequiredId(this IUserContext userContext)
        => userContext.Id ?? throw new AuthenticationException("用户未登录");
}

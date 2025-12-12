using System.Security.Authentication;

namespace NOF;

public static partial class __NOF_Contract_Extensions__
{
    public static string GetRequiredId(this IUserContext userContext)
        => userContext.Id ?? throw new AuthenticationException("用户未登录");
}

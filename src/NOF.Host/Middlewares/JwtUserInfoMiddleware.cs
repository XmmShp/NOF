using Microsoft.AspNetCore.Http;
using System.Security.Authentication;
using System.Security.Claims;

namespace NOF;

/// <summary>
/// JWT用户信息中间件，用于从JWT令牌中提取用户信息并设置到UserContext中
/// </summary>
public class JwtUserInfoMiddleware : IMiddleware
{
    private readonly IUserContext _userContext;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="userContext">用户信息上下文</param>
    public JwtUserInfoMiddleware(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _userContext.SetUserAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new AuthenticationException("用户未登录"),
                context.User.FindFirstValue(ClaimTypes.Name) ?? throw new AuthenticationException("用户未登录"),
                context.User.FindAll(ClaimTypes.Role).Select(c => c.Value));
        }

        await next(context);
    }
}

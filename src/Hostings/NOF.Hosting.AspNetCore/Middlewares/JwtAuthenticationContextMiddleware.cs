using Microsoft.AspNetCore.Http;
using System.Security.Authentication;
using System.Security.Claims;

namespace NOF;

/// <summary>
/// 认证上下文中间件，用于从认证信息中提取用户和租户信息并设置到相应的上下文中
/// </summary>
public class JwtAuthenticationContextMiddleware : IMiddleware
{
    private readonly IUserContextInternal _userContext;
    private readonly ITenantContextInternal _tenantContext;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="userContext">用户信息上下文</param>
    /// <param name="tenantContext">租户信息上下文</param>
    public JwtAuthenticationContextMiddleware(IUserContextInternal userContext, ITenantContextInternal tenantContext)
    {
        _userContext = userContext;
        _tenantContext = tenantContext;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // 提取用户基本信息
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = context.User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
            {
                throw new AuthenticationException("用户身份信息不完整");
            }

            // 提取权限信息
            var permissions = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value);

            // 设置用户上下文
            await _userContext.SetUserAsync(userId, username, permissions);

            // 将其他用户信息存储到 Properties 中
            var userClaims = context.User.Claims.ToList();
            foreach (var claim in userClaims)
            {
                // 跳过已经处理的基本信息
                if (claim.Type == ClaimTypes.NameIdentifier ||
                    claim.Type == ClaimTypes.Name ||
                    claim.Type == ClaimTypes.Role)
                {
                    continue;
                }

                // 将其他声明存储到 Properties 中
                _userContext.Properties[claim.Type] = claim.Value;
            }

            // 提取租户信息
            var tenantId = context.User.FindFirstValue(NOFConstants.TenantId);
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new AuthenticationException("Tenant ID is required");
            }

            // 设置租户上下文
            _tenantContext.SetCurrentTenantId(tenantId);
        }
        else
        {
            // 未认证状态下清空上下文
            await _userContext.UnsetUserAsync();
            _tenantContext.SetCurrentTenantId("default");
        }

        await next(context);
    }
}

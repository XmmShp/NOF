using Microsoft.AspNetCore.Http;
using System.Security.Authentication;
using System.Security.Claims;

namespace NOF;

/// <summary>
/// 认证上下文中间件，用于从认证信息中提取用户和租户信息并设置到 InvocationContext 中
/// </summary>
public class InvocationContextMiddleware : IMiddleware
{
    private readonly IInvocationContextInternal _invocationContext;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="invocationContext">调用上下文</param>
    public InvocationContextMiddleware(IInvocationContextInternal invocationContext)
    {
        _invocationContext = invocationContext;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // 设置用户上下文
            await _invocationContext.SetUserAsync(context.User);

            // 提取租户信息
            var tenantId = context.User.FindFirstValue(NOFConstants.TenantId);
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new AuthenticationException("Tenant ID is required");
            }

            // 设置租户上下文
            _invocationContext.SetTenantId(tenantId);
        }
        else
        {
            // 未认证状态下清空上下文
            await _invocationContext.UnsetUserAsync();
            _invocationContext.SetTenantId("default");
        }

        await next(context);
    }
}

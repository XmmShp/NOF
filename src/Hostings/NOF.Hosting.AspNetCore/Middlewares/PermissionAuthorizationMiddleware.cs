using Microsoft.AspNetCore.Http;

namespace NOF;

/// <summary>
/// 权限授权中间件
/// 处理被 <see cref="RequirePermissionAttribute"/> 标记的控制器或操作方法
/// </summary>
public class PermissionAuthorizationMiddleware : IMiddleware
{
    private readonly IUserContext _userContext;

    /// <summary>
    /// 初始化 <see cref="PermissionAuthorizationMiddleware"/> 类的新实例
    /// </summary>
    /// <param name="userContext">用户上下文</param>
    public PermissionAuthorizationMiddleware(IUserContext userContext)
    {
        _userContext = userContext;
    }

    /// <summary>
    /// 处理HTTP请求
    /// </summary>
    /// <param name="context">HTTP上下文</param>
    /// <param name="next">请求委托</param>
    /// <returns>任务</returns>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null)
        {
            await next(context);
            return;
        }

        var permissionAttr = endpoint.Metadata.GetMetadata<RequirePermissionAttribute>();

        if (permissionAttr is not null)
        {
            if (!_userContext.IsAuthenticated)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("请先登录");
                return;
            }

            if (!string.IsNullOrEmpty(permissionAttr.Permission)
                && !_userContext.HasPermission(permissionAttr.Permission))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("没有所需的权限");
                return;
            }
        }

        await next(context);
    }
}

namespace NOF;

/// <summary>
/// HTTP请求方法枚举
/// </summary>
public enum HttpVerb
{
    Get,
    Post,
    Put,
    Delete,
    Patch
}

/// <summary>
/// 控制器端点特性，用于自动生成Controller端点方法
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class HttpEndpointAttribute : Attribute
{
    /// <summary>
    /// HTTP请求方法
    /// </summary>
    public HttpVerb Method { get; }

    /// <summary>
    /// 一级路由，如果为null则不使用
    /// </summary>
    public string? Group { get; }

    /// <summary>
    /// 端点路径，如果为null则使用Request类型名称（去掉"Request"后缀）
    /// </summary>
    public string? Route { get; }

    /// <summary>
    /// 权限名称，如果不为null则生成RequirePermissionAttribute
    /// </summary>
    public string? Permission { get; set; }

    /// <summary>
    /// 是否允许匿名访问，默认为false
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// 创建控制器端点特性的新实例
    /// </summary>
    /// <param name="method">HTTP请求方法</param>
    /// <param name="group">一级路由，如果为null则不使用</param>
    /// <param name="route">端点路径，如果为null则使用Request类型名称（去掉"Request"后缀）</param>
    public HttpEndpointAttribute(HttpVerb method, string? group = null, string? route = null)
    {
        Method = method;
        Group = group;
        Route = route;
        AllowAnonymous = false;
    }
}

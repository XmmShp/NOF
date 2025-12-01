namespace NOF;

/// <summary>
/// 标记一个请求类型，将其暴露为HTTP端点
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class ExposeToHttpEndpointAttribute : Attribute
{
    /// <summary>
    /// HTTP方法
    /// </summary>
    public HttpVerb Method { get; }

    /// <summary>
    /// 路由模板
    /// </summary>
    public string? Route { get; }

    /// <summary>
    /// 操作名称，用于生成客户端方法名。如果为null，则使用请求类型名（去掉"Request"后缀）
    /// </summary>
    public string? OperationName { get; init; }

    /// <summary>
    /// 权限标识符
    /// </summary>
    public string? Permission { get; init; }

    /// <summary>
    /// 是否允许匿名访问
    /// </summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>
    /// 创建一个新的 ExposeToHttpEndpointAttribute 实例
    /// </summary>
    /// <param name="method">HTTP方法</param>
    /// <param name="route">路由模板</param>
    public ExposeToHttpEndpointAttribute(HttpVerb method, string? route = null)
    {
        Method = method;
        Route = route;
    }
}

public enum HttpVerb
{
    Get,
    Post,
    Put,
    Delete,
    Patch
}

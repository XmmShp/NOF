namespace NOF;

public enum Lifetime
{
    Singleton = 0,
    Scoped = 1,
    Transient = 2
}

/// <summary>
/// 服务提供者特性，用于标记可自动注册的服务类
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AutoInjectAttribute : Attribute
{
    /// <summary> 
    /// 服务生命周期，定义服务实例的作用域
    /// </summary>
    public Lifetime Lifetime { get; }

    /// <summary>
    /// 要注册的服务类型数组，如果为空则自动使用该类实现的所有接口
    /// </summary>
    public Type[]? RegisterTypes { get; set; }

    /// <summary>
    /// 创建服务提供者特性的新实例
    /// </summary>
    /// <param name="lifetime">服务生命周期</param>
    public AutoInjectAttribute(Lifetime lifetime)
    {
        Lifetime = lifetime;
    }
}

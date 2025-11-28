namespace NOF;

/// <summary>
/// 失败定义特性，用于标记失败类型并自动生成静态失败实例
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class FailureAttribute : Attribute
{
    /// <summary>
    /// 失败名称（用作静态字段名）
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 失败消息
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 失败代码
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// 创建失败定义特性的新实例
    /// </summary>
    /// <param name="name">失败名称</param>
    /// <param name="message">失败消息</param>
    /// <param name="errorCode">失败代码</param>
    public FailureAttribute(string name, string message, int errorCode)
    {
        Name = name;
        Message = message;
        ErrorCode = errorCode;
    }
}

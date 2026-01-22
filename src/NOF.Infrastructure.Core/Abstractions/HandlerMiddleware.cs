using NOF.Contract.Annotations;
using System.ComponentModel;

namespace NOF;

/// <summary>
/// Handler 执行上下文
/// 包含 Handler 执行过程中的元数据
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HandlerContext
{
    /// <summary>
    /// 消息实例
    /// </summary>
    public required IMessage Message { get; init; }

    /// <summary>
    /// Handler 实例
    /// </summary>
    public required IMessageHandler Handler { get; init; }

    /// <summary>
    /// 自定义属性字典，用于在中间件之间传递数据
    /// </summary>
    public Dictionary<string, object?> Items { get; init; } = new();

    /// <summary>
    /// Handler 类型名称
    /// </summary>
    public string HandlerType
    {
        get
        {
            var type = Handler.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(StateMachineNotificationHandler<,>))
            {
                var stateMachineType = type.GenericTypeArguments[0];
                return stateMachineType.Name;
            }

            return Handler.GetType().Name;
        }
    }

    /// <summary>
    /// 消息类型名称
    /// </summary>
    public string MessageType => Message.GetType().Name;
}

/// <summary>
/// Handler 执行管道的委托
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask HandlerDelegate(CancellationToken cancellationToken);

/// <summary>
/// Handler 中间件接口
/// 用于在 Handler 执行前后插入横切关注点（如事务、日志、验证等）
/// </summary>
public interface IHandlerMiddleware
{
    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    /// <param name="context">Handler 执行上下文</param>
    /// <param name="next">管道中的下一个中间件或最终的 Handler</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

public static partial class NOFConstants
{
    public const string MessageId = "NOF.Message.MessageId";
    public const string TraceId = "NOF.Message.TraceId";
    public const string SpanId = "NOF.Message.SpanId";
}

public static partial class __NOF_Infrastructure_Core_Extensions__
{
    extension(HandlerContext context)
    {
        public Guid MessageId
        {
            get
            {
                if (context.Items.TryGetValue(NOFConstants.MessageId, out var value) && value is Guid guidValue)
                {
                    return guidValue;
                }

                if (value is string stringValue)
                {
                    guidValue = Guid.Parse(stringValue);
                }
                else
                {
                    guidValue = Guid.NewGuid();
                }

                context.Items[NOFConstants.MessageId] = guidValue;
                return guidValue;
            }

            set => context.Items[NOFConstants.MessageId] = value;
        }
    }
}
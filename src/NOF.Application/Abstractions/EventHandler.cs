using NOF.Application.Annotations;

namespace NOF;

public interface IEventHandler<in TEvent> : IEventHandler
    where TEvent : class, IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}

/// <summary>
/// EventHandler 基类，提供事务性消息发送能力
/// 无需注入任何依赖，通过 AsyncLocal 自动工作
/// </summary>
public abstract class EventHandler<TEvent> : HandlerBase, IEventHandler<TEvent>
    where TEvent : class, IEvent
{
    public abstract Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
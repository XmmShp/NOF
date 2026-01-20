using System.ComponentModel;
using System.Diagnostics;

namespace NOF;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineContextRepository
{
    ValueTask<StateMachineInfo?> FindAsync(string correlationId, Type definitionType);
    void Add(StateMachineInfo stateMachineInfo);
    void Update(StateMachineInfo stateMachineInfo);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class StateMachineNotificationHandler<TStateMachineDefinition, TNotification> : INotificationHandler<TNotification>
    where TStateMachineDefinition : class, IStateMachineDefinition
    where TNotification : class, INotification
{
    private readonly IStateMachineContextRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStateMachineRegistry _stateMachineRegistry;

    public StateMachineNotificationHandler(
        IStateMachineContextRepository repository,
        IUnitOfWork uow,
        IServiceProvider serviceProvider,
        IStateMachineRegistry stateMachineRegistry)
    {
        _repository = repository;
        _uow = uow;
        _serviceProvider = serviceProvider;
        _stateMachineRegistry = stateMachineRegistry;
    }

    public async Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        var blueprints = _stateMachineRegistry.GetBlueprints<TNotification>();
        foreach (var bp in blueprints)
        {
            var correlationId = bp.GetCorrelationId(notification);
            ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

            // 使用CorrelationId作为Handler名称，而不是类型名
            using var instanceActivity = StateMachineTracing.Source.StartActivity(
                name: $"{StateMachineTracing.ActivityNames.Handler}({correlationId})",
                kind: ActivityKind.Consumer);

            if (instanceActivity != null)
            {
                instanceActivity.SetTag(StateMachineTracing.Tags.CorrelationId, correlationId);
                instanceActivity.SetTag(StateMachineTracing.Tags.Type, bp.DefinitionType.Name);
                instanceActivity.SetTag(StateMachineTracing.Tags.HandlerName, $"{bp.DefinitionType.Name}({correlationId})");

                // 设置当前追踪信息
                var currentActivity = Activity.Current;
                if (currentActivity != null)
                {
                    instanceActivity.SetTag(StateMachineTracing.Tags.TraceId, currentActivity.TraceId.ToString());
                    instanceActivity.SetTag(StateMachineTracing.Tags.SpanId, currentActivity.SpanId.ToString());
                }

                // 设置Baggage以便后续追踪
                instanceActivity.SetBaggage(StateMachineTracing.Baggage.CorrelationId, correlationId);
                instanceActivity.SetBaggage(StateMachineTracing.Baggage.Type, bp.DefinitionType.FullName);
            }

            try
            {
                var existing = await _repository.FindAsync(correlationId, bp.DefinitionType);
                if (existing is not null)
                {
                    var context = new StatefulStateMachineContext
                    {
                        Context = existing.Context,
                        State = existing.State
                    };
                    await bp.TransferAsync(context, notification, _serviceProvider, cancellationToken);

                    // 创建更新后的状态机上下文
                    var updatedContext = StateMachineInfo.Create(
                        correlationId: correlationId,
                        definitionType: bp.DefinitionType,
                        context: context.Context,
                        state: context.State,
                        traceId: existing.TraceId,
                        spanId: existing.SpanId);

                    _repository.Update(updatedContext);
                }
                else
                {
                    var context = await bp.StartAsync(notification, _serviceProvider, cancellationToken);
                    if (context is not null)
                    {
                        // 捕获当前追踪上下文
                        var currentActivity = Activity.Current;
                        var newStateMachineContext = StateMachineInfo.Create(
                            correlationId: correlationId,
                            definitionType: bp.DefinitionType,
                            context: context.Context,
                            state: context.State,
                            traceId: currentActivity?.TraceId.ToString(),
                            spanId: currentActivity?.SpanId.ToString());

                        _repository.Add(newStateMachineContext);
                    }
                }
            }
            catch (Exception ex)
            {
                instanceActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

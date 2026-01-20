using System.ComponentModel;
using System.Diagnostics;

namespace NOF;

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
                    state: context.State);

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

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

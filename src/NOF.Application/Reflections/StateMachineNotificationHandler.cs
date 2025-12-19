using NOF.Application.Dependents;
using NOF.Application.Integrations;

namespace NOF.Application.Reflections;

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
                var context = new StatefulStateMachineContext { Context = existing.Value.Context, State = existing.Value.State };
                await bp.TransferAsync(context, notification, _serviceProvider, cancellationToken);
                _repository.Update(correlationId, bp.DefinitionType, context.Context, context.State);
            }
            else
            {
                var context = await bp.StartAsync(notification, _serviceProvider, cancellationToken);
                if (context is not null)
                {
                    _repository.Add(correlationId, bp.DefinitionType, context.Context, context.State);
                }
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

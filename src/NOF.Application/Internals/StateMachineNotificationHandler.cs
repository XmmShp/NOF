using Microsoft.Extensions.DependencyInjection;

namespace NOF.Application.Internals;

public sealed class StateMachineNotificationHandler<TStateMachineDefinition, TNotification> : INotificationHandler<TNotification>
    where TStateMachineDefinition : class, IStateMachineDefinition
    where TNotification : class, INotification
{
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IStateMachineContextRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStateMachineRegistry _stateMachineRegistry;

    public StateMachineNotificationHandler(
        ICorrelationIdProvider correlationIdProvider,
        IStateMachineContextRepository repository,
        IUnitOfWork uow,
        IServiceProvider serviceProvider,
        IStateMachineRegistry stateMachineRegistry)
    {
        _correlationIdProvider = correlationIdProvider;
        _repository = repository;
        _uow = uow;
        _serviceProvider = serviceProvider;
        _stateMachineRegistry = stateMachineRegistry;
    }

    public async Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdProvider.CorrelationId;
        var existing = await _repository.FindAsync(correlationId);

        if (existing is not null)
        {
            var (contextType, context) = existing.Value;
            await ExecuteAsync(contextType, notification, context, cancellationToken);
            _repository.Update(context);
        }
        else
        {
            var createdContext = await CreateStateMachineContextAndExecuteAsync(correlationId, notification, cancellationToken);
            if (createdContext is not null)
            {
                _repository.Add(createdContext);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task<IStateMachineContext?> CreateStateMachineContextAndExecuteAsync(string correlationId, TNotification notification, CancellationToken cancellationToken)
    {
        var startupOps = _stateMachineRegistry.GetStartupOperations(typeof(TNotification));

        foreach (var op in startupOps)
        {
            var contextType = op.ContextType;
            if (ActivatorUtilities.CreateInstance(_serviceProvider, contextType) is not IStateMachineContext context)
            {
                continue; // should not happen due to 'new()' constraint
            }

            try
            {
                context.CorrelationId = correlationId;
                await op.ExecuteAsync(context, notification, _serviceProvider, cancellationToken);
                return context;
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        return null;
    }

    private async Task ExecuteAsync(Type contextType, TNotification notification, IStateMachineContext context, CancellationToken cancellationToken)
    {
        var transferOps = _stateMachineRegistry.GetTransferOperations(typeof(TNotification), contextType);

        foreach (var op in transferOps)
        {
            await op.ExecuteAsync(context, notification, _serviceProvider, cancellationToken);
        }
    }
}

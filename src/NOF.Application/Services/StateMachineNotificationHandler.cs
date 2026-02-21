using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics;

namespace NOF.Application;

/// <summary>
/// Handles notifications by routing them to the appropriate state machine instances. Not intended for direct use.
/// Subclassed by source-generated concrete handler types per state machine definition.
/// </summary>
/// <typeparam name="TStateMachineDefinition">The state machine definition type.</typeparam>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TContext">The state machine context type.</typeparam>
/// <typeparam name="TNotification">The notification type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class StateMachineNotificationHandler<TStateMachineDefinition, TState, TContext, TNotification> : INotificationHandler<TNotification>
    where TStateMachineDefinition : IStateMachineDefinition<TState, TContext>, new()
    where TState : struct, Enum
    where TContext : class
    where TNotification : class, INotification
{
    private readonly IStateMachineContextRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStateMachineRegistry _stateMachineRegistry;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="repository">The state machine context repository.</param>
    /// <param name="uow">The unit of work.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="stateMachineRegistry">The state machine registry.</param>
    protected StateMachineNotificationHandler(
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

    private static StateMachineBlueprint CreateBlueprint()
    {
        var builder = new StateMachineBuilder<TState, TContext>();
        new TStateMachineDefinition().Build(builder);
        var blueprint = ((IStateMachineBuilderInternal)builder).Build();
        blueprint.DefinitionType = typeof(TStateMachineDefinition);
        return blueprint;
    }

    /// <inheritdoc />
    public async Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        var bp = _stateMachineRegistry.GetBlueprint(
            typeof(TStateMachineDefinition),
            typeof(TNotification),
            CreateBlueprint);

        if (bp is null)
        {
            return;
        }

        var correlationId = bp.GetCorrelationId(notification);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        Activity.Current?.SetTag("correlationId", correlationId);

        var existing = await _repository.FindAsync(correlationId, typeof(TStateMachineDefinition));
        if (existing is not null)
        {
            var context = new StatefulStateMachineContext
            {
                Context = existing.Context,
                State = existing.State
            };
            await bp.TransferAsync(context, notification, _serviceProvider, cancellationToken);

            // Create the updated state machine context
            var updatedContext = StateMachineContext.Create(
                correlationId: correlationId,
                definitionType: typeof(TStateMachineDefinition),
                context: context.Context,
                state: context.State);

            _repository.Update(updatedContext);
        }
        else
        {
            var context = await bp.StartAsync(notification, _serviceProvider, cancellationToken);
            if (context is not null)
            {
                // Capture the current tracing context
                var currentActivity = Activity.Current;
                var newStateMachineContext = StateMachineContext.Create(
                    correlationId: correlationId,
                    definitionType: typeof(TStateMachineDefinition),
                    context: context.Context,
                    state: context.State,
                    traceId: currentActivity?.TraceId.ToString(),
                    spanId: currentActivity?.SpanId.ToString());

                _repository.Add(newStateMachineContext);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

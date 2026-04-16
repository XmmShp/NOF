using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Handles notifications by routing them to the appropriate state machine instances. Not intended for direct use.
/// Subclassed by source-generated concrete handler types per state machine definition.
/// </summary>
/// <typeparam name="TStateMachineDefinition">The state machine definition type.</typeparam>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TNotification">The notification type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class StateMachineNotificationHandler<TStateMachineDefinition, TState, TNotification> : NotificationHandler<TNotification>
    where TStateMachineDefinition : IStateMachineDefinition<TState>, new()
    where TState : struct, Enum
    where TNotification : class
{
    private readonly DbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStateMachineRegistry _stateMachineRegistry;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="dbContext">The current persistence context.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="stateMachineRegistry">The state machine registry.</param>
    protected StateMachineNotificationHandler(
        DbContext dbContext,
        IServiceProvider serviceProvider,
        IStateMachineRegistry stateMachineRegistry)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
        _stateMachineRegistry = stateMachineRegistry;
    }

    private static StateMachineBlueprint CreateBlueprint()
    {
        var builder = new BuildableStateMachineBuilder<TState>();
        new TStateMachineDefinition().Build(builder);
        var blueprint = builder.Build();
        blueprint.DefinitionType = typeof(TStateMachineDefinition);
        return blueprint;
    }

    /// <inheritdoc />
    public override async Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
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
        var definitionTypeName = typeof(TStateMachineDefinition).FullName;
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionTypeName);

        var existing = await _dbContext.FindAsync<NOFStateMachineContext>(
            keyValues: [correlationId, definitionTypeName],
            cancellationToken: cancellationToken);

        if (existing is not null)
        {
            var newState = await bp.TransferAsync(existing.State, notification, _serviceProvider, cancellationToken);
            existing.State = newState;
        }
        else
        {
            var initialState = await bp.StartAsync(notification, _serviceProvider, cancellationToken);
            if (initialState.HasValue)
            {
                _dbContext.Set<NOFStateMachineContext>().Add(NOFStateMachineContext.Create(
                    correlationId: correlationId,
                    definitionTypeName: definitionTypeName,
                    state: initialState.Value));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

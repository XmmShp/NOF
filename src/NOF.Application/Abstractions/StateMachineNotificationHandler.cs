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
/// <typeparam name="TNotification">The notification type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class StateMachineNotificationHandler<TStateMachineDefinition, TState, TNotification> : INotificationHandler<TNotification>
    where TStateMachineDefinition : IStateMachineDefinition<TState>, new()
    where TState : struct, Enum
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
        var builder = new BuildableStateMachineBuilder<TState>();
        new TStateMachineDefinition().Build(builder);
        var blueprint = builder.Build();
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
        var definitionTypeName = typeof(TStateMachineDefinition).FullName;
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionTypeName);

        var existing = await _repository.FindAsync(correlationId, definitionTypeName);

        var originalActivity = Activity.Current;

        Activity? childActivity = null;
        Activity? standaloneActivity = null;
        int? oldState = null;
        int? newState = null;
        int? initialState = null;

        try
        {
            Activity.Current = originalActivity;
            childActivity = CreateChildActivity(existing, correlationId, definitionTypeName);

            Activity.Current = null;
            standaloneActivity = CreateStandaloneActivity(existing, correlationId, definitionTypeName);

            if (childActivity is not null && standaloneActivity is not null)
            {
                childActivity.AddLink(new ActivityLink(standaloneActivity.Context));
                standaloneActivity.AddLink(new ActivityLink(childActivity.Context));
            }

            Activity.Current = originalActivity;

            if (existing is not null)
            {
                oldState = existing.State;
                newState = await bp.TransferAsync(existing.State, notification, _serviceProvider, cancellationToken);
                existing.State = newState.Value;
            }
            else
            {
                initialState = await bp.StartAsync(notification, _serviceProvider, cancellationToken);
            }

            if (childActivity is not null)
            {
                if (oldState.HasValue)
                {
                    childActivity.SetTag(NOFApplicationConstants.StateMachine.Tags.FromState, oldState.Value.ToString());
                }
                childActivity.SetTag(NOFApplicationConstants.StateMachine.Tags.ToState, (newState ?? initialState)!.Value.ToString());
            }

            if (standaloneActivity is not null)
            {
                if (oldState.HasValue)
                {
                    standaloneActivity.SetTag(NOFApplicationConstants.StateMachine.Tags.FromState, oldState.Value.ToString());
                }
                standaloneActivity.SetTag(NOFApplicationConstants.StateMachine.Tags.ToState, (newState ?? initialState)!.Value.ToString());

                if (standaloneActivity.TraceId != default && standaloneActivity.SpanId != default)
                {
                    var tracingInfo = new TracingInfo(standaloneActivity.TraceId.ToString(), standaloneActivity.SpanId.ToString());
                    if (existing is not null)
                    {
                        existing.TracingInfo = tracingInfo;
                    }
                    else if (initialState.HasValue)
                    {
                        _repository.Add(NOFStateMachineContext.Create(
                            correlationId: correlationId,
                            definitionTypeName: definitionTypeName,
                            state: initialState.Value,
                            tracingInfo: tracingInfo));
                    }
                }
                else if (existing is null && initialState.HasValue)
                {
                    _repository.Add(NOFStateMachineContext.Create(
                        correlationId: correlationId,
                        definitionTypeName: definitionTypeName,
                        state: initialState.Value,
                        tracingInfo: null));
                }
            }
            else if (existing is null && initialState.HasValue)
            {
                _repository.Add(NOFStateMachineContext.Create(
                    correlationId: correlationId,
                    definitionTypeName: definitionTypeName,
                    state: initialState.Value,
                    tracingInfo: null));
            }

            await _uow.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            childActivity?.Dispose();
            standaloneActivity?.Dispose();
            Activity.Current = originalActivity;
        }
    }

    private Activity? CreateChildActivity(NOFStateMachineContext? existing, string correlationId, string definitionTypeName)
    {
        var activity = NOFApplicationConstants.StateMachine.Source.StartActivity(
            NOFApplicationConstants.StateMachine.ActivityNames.StateTransition,
            ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag(NOFApplicationConstants.StateMachine.Tags.CorrelationId, correlationId);
            activity.SetTag(NOFApplicationConstants.StateMachine.Tags.DefinitionType, definitionTypeName);
            activity.SetTag(NOFApplicationConstants.StateMachine.Tags.NotificationType, typeof(TNotification).FullName);
            activity.SetTag("state_machine.link_type", "child");
        }

        return activity;
    }

    private Activity? CreateStandaloneActivity(NOFStateMachineContext? existing, string correlationId, string definitionTypeName)
    {
        ActivityContext? parentContext = null;

        if (existing?.TracingInfo is not null)
        {
            try
            {
                var traceId = ActivityTraceId.CreateFromString(existing.TracingInfo.TraceId.AsSpan());
                var spanId = ActivitySpanId.CreateFromString(existing.TracingInfo.SpanId.AsSpan());
                parentContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
            }
            catch
            {
            }
        }

        var activity = NOFApplicationConstants.StateMachine.Source.StartActivity(
            NOFApplicationConstants.StateMachine.ActivityNames.StateTransition,
            ActivityKind.Internal,
            parentContext ?? default);

        if (activity is not null)
        {
            activity.SetTag(NOFApplicationConstants.StateMachine.Tags.CorrelationId, correlationId);
            activity.SetTag(NOFApplicationConstants.StateMachine.Tags.DefinitionType, definitionTypeName);
            activity.SetTag(NOFApplicationConstants.StateMachine.Tags.NotificationType, typeof(TNotification).FullName);
            activity.SetTag("state_machine.link_type", "standalone");
        }

        return activity;
    }
}

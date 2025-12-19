using Microsoft.Extensions.Logging;
using NOF.Application.Internals;

namespace NOF.Sample;

public enum SampleState
{
    Processing,
    Completed,
    Failed
}

public record TaskStarted : INotification;
public record ProcessingSucceeded : INotification;
public record ProcessingFailed(string CorrelationId, string Reason) : INotification;

public record StartProcessingCommand(string CorrelationId) : ICommand;

public class SampleStateMachineContext : IStateMachineContext<SampleState>
{
    public string CorrelationId { get; set; }
    public SampleState State { get; set; }
}

public class StartProcessingCommandHandler : ICommandHandler<StartProcessingCommand>
{
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<StartProcessingCommandHandler> _logger;

    public StartProcessingCommandHandler(INotificationPublisher notificationPublisher, IUnitOfWork uow, ILogger<StartProcessingCommandHandler> logger)
    {
        _notificationPublisher = notificationPublisher;
        _uow = uow;
        _logger = logger;
    }

    public async Task HandleAsync(StartProcessingCommand command, CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);
        var isSuccess = Random.Shared.Next(2) == 0;
        if (isSuccess)
        {
            _logger.LogInformation("Processing {Id} Succeeded", command.CorrelationId);
            await _notificationPublisher.PublishAsync(new ProcessingSucceeded(), cancellationToken);
        }
        else
        {
            _logger.LogError("Processing {Id} Failed", command.CorrelationId);
            await _notificationPublisher.PublishAsync(new ProcessingFailed(command.CorrelationId, "An error occurred during processing."), cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

public class SampleStateMachine : IStateMachineDefinition<SampleState, SampleStateMachineContext>
{
    public void Build(IStateMachineBuilder<SampleState, SampleStateMachineContext> builder)
    {
        builder.StartWhen<TaskStarted>(SampleState.Processing)
            .Execute((ctx, _, _) => Console.WriteLine($"[{ctx.CorrelationId}] Task ."))
            .SendCommandAsync((ctx, _) => new StartProcessingCommand(ctx.CorrelationId));

        builder.On(SampleState.Processing)
            .When<ProcessingSucceeded>()
            .Execute((ctx, _, _) => Console.WriteLine($"[{ctx.CorrelationId}] Task succeeded!"))
            .TransitionTo(SampleState.Completed);

        builder.On(SampleState.Processing)
            .When<ProcessingFailed>()
            .Execute((ctx, notification, _) =>
            {
                Console.WriteLine($"[{ctx.CorrelationId}] Failed: {notification.Reason}");
            })
            .TransitionTo(SampleState.Failed);
    }
}

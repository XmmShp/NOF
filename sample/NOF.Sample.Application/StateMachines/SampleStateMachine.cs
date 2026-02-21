using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NOF.Application;
using NOF.Contract;

namespace NOF.Sample;

public enum SampleState
{
    Processing,
    Completed,
    Failed,
    Stopped
}

public record TaskStarted(string TaskId) : INotification;
public record TaskContinued(string TaskId) : INotification;

public record ProcessingSucceeded(string TaskId) : INotification;
public record ProcessingFailed(string TaskId, string Reason) : INotification;
public record StartProcessingCommand(string TaskId) : ICommand;

public class SampleStateMachineContext
{
    public string TaskId { get; set; } = null!;
    public DateTime StartOn { get; set; }
    public DateTime? SucceededOn { get; set; }
    public DateTime? FailedOn { get; set; }
    public string FailReason { get; set; } = null!;
}

public class StartProcessingCommandHandler : ICommandHandler<StartProcessingCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IDeferredNotificationPublisher _notificationPublisher;
    private readonly ILogger<StartProcessingCommandHandler> _logger;

    public StartProcessingCommandHandler(
        IUnitOfWork uow,
        IDeferredNotificationPublisher notificationPublisher,
        ILogger<StartProcessingCommandHandler> logger)
    {
        _uow = uow;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    public async Task HandleAsync(StartProcessingCommand command, CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);
        var isSuccess = Random.Shared.Next(2) == 0;
        if (isSuccess)
        {
            _logger.LogInformation("Processing {Id} Succeeded", command.TaskId);
            _notificationPublisher.Publish(new ProcessingSucceeded(command.TaskId));
        }
        else
        {
            _logger.LogError("Processing {Id} Failed", command.TaskId);
            _notificationPublisher.Publish(new ProcessingFailed(command.TaskId, "An error occurred during processing."));
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

public class SampleStateMachine : IStateMachineDefinition<SampleState>
{
    private static string TaskKey(string taskId) => $"Task-{taskId}";

    public void Build(IStateMachineBuilder<SampleState> builder)
    {
        builder.Correlate<TaskStarted>(n => TaskKey(n.TaskId));
        builder.Correlate<TaskContinued>(n => TaskKey(n.TaskId));
        builder.Correlate<ProcessingSucceeded>(n => TaskKey(n.TaskId));
        builder.Correlate<ProcessingFailed>(n => TaskKey(n.TaskId));

        builder.StartWhen<TaskStarted>(SampleState.Processing)
            .SendCommandAsync(notification => new StartProcessingCommand(notification.TaskId));

        builder.On(SampleState.Processing)
            .When<ProcessingSucceeded>()
            .TransitionTo(SampleState.Completed);

        builder.On(SampleState.Processing)
                .When<ProcessingFailed>()
                .TransitionTo(SampleState.Failed);

        builder.On(SampleState.Completed)
            .When<TaskContinued>()
            .Execute((n, sp) =>
            {
                sp.GetRequiredService<ILogger<SampleStateMachine>>()
                    .LogInformation("Task-{TaskId} Continued.", n.TaskId);
            })
            .TransitionTo(SampleState.Stopped);

        builder.On(SampleState.Failed)
            .When<TaskContinued>()
            .Execute((n, sp) =>
            {
                sp.GetRequiredService<ILogger<SampleStateMachine>>()
                    .LogInformation("Task-{TaskId} Continued.", n.TaskId);
            })
            .TransitionTo(SampleState.Stopped);
    }
}

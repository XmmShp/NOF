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

public record TaskStarted(string TaskId);
public record TaskContinued(string TaskId);

public record ProcessingSucceeded(string TaskId);
public record ProcessingFailed(string TaskId, string Reason);
public record StartProcessingCommand(string TaskId);

public class StartProcessingCommandHandler : CommandHandler<StartProcessingCommand>
{
    private readonly Microsoft.EntityFrameworkCore.DbContext _dbContext;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<StartProcessingCommandHandler> _logger;

    public StartProcessingCommandHandler(
        Microsoft.EntityFrameworkCore.DbContext dbContext,
        INotificationPublisher notificationPublisher,
        ILogger<StartProcessingCommandHandler> logger)
    {
        _dbContext = dbContext;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    public override async Task HandleAsync(StartProcessingCommand command, CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);
        var isSuccess = Random.Shared.Next(2) == 0;
        if (isSuccess)
        {
            _logger.LogInformation("Processing {Id} Succeeded", command.TaskId);
            _notificationPublisher.DeferPublish(new ProcessingSucceeded(command.TaskId));
        }
        else
        {
            _logger.LogError("Processing {Id} Failed", command.TaskId);
            _notificationPublisher.DeferPublish(new ProcessingFailed(command.TaskId, "An error occurred during processing."));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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

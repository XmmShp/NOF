using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

public class SampleStateMachineContext : IStateMachineContext
{
    public string TaskId { get; set; }
    public DateTime StartOn { get; set; }
    public DateTime? SucceededOn { get; set; }
    public DateTime? FailedOn { get; set; }
    public string FailReason { get; set; }
}

public class StartProcessingCommandHandler : CommandHandler<StartProcessingCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<StartProcessingCommandHandler> _logger;

    public StartProcessingCommandHandler(IUnitOfWork uow, ILogger<StartProcessingCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public override async Task HandleAsync(StartProcessingCommand command, CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);
        var isSuccess = Random.Shared.Next(2) == 0;
        if (isSuccess)
        {
            _logger.LogInformation("Processing {Id} Succeeded", command.TaskId);
            PublishNotification(new ProcessingSucceeded(command.TaskId));
        }
        else
        {
            _logger.LogError("Processing {Id} Failed", command.TaskId);
            PublishNotification(new ProcessingFailed(command.TaskId, "An error occurred during processing."));
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

public class SampleStateMachine : IStateMachineDefinition<SampleState, SampleStateMachineContext>
{
    private string TaskKey(string taskId) => $"Task-{taskId}";

    public void Build(IStateMachineBuilder<SampleState, SampleStateMachineContext> builder)
    {
        builder.Correlate<TaskStarted>(n => TaskKey(n.TaskId));
        builder.Correlate<TaskContinued>(n => TaskKey(n.TaskId));
        builder.Correlate<ProcessingSucceeded>(n => TaskKey(n.TaskId));
        builder.Correlate<ProcessingFailed>(n => TaskKey(n.TaskId));

        builder.StartWhen<TaskStarted>(
                SampleState.Processing,
                n => new SampleStateMachineContext
                {
                    StartOn = DateTime.UtcNow,
                    TaskId = n.TaskId
                })
            .SendCommandAsync((_, notification) => new StartProcessingCommand(notification.TaskId));

        builder.On(SampleState.Processing)
            .When<ProcessingSucceeded>()
            .Modify((ctx, _) => ctx.SucceededOn = DateTime.UtcNow)
            .TransitionTo(SampleState.Completed);

        builder.On(SampleState.Processing)
                .When<ProcessingFailed>()
                .Modify((ctx, notification) =>
                {
                    ctx.FailedOn = DateTime.UtcNow;
                    ctx.FailReason = notification.Reason;
                })
                .TransitionTo(SampleState.Failed);

        builder.On(SampleState.Completed)
            .When<TaskContinued>()
            .Execute((_, n, sp) =>
            {
                sp.GetRequiredService<ILogger<SampleStateMachine>>()
                    .LogInformation("Task-{TaskId} Continued.", n.TaskId);
            })
            .TransitionTo(SampleState.Stopped);

        builder.On(SampleState.Failed)
            .When<TaskContinued>()
            .Execute((_, n, sp) =>
            {
                sp.GetRequiredService<ILogger<SampleStateMachine>>()
                    .LogInformation("Task-{TaskId} Continued.", n.TaskId);
            })
            .TransitionTo(SampleState.Stopped);
    }
}

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public interface IStateMachineBuilderWhenClause<in TState, out TNotification>
    where TNotification : class
    where TState : struct, Enum
{
    IStateMachineBuilderWhenClause<TState, TNotification> ExecuteAsync(Func<TNotification, IServiceProvider, CancellationToken, Task> actionFunc);
    void TransitionTo(TState state);
}

public static partial class NOFApplicationExtensions
{
    extension<TState, TNotification>(IStateMachineBuilderWhenClause<TState, TNotification> clause)
        where TNotification : class
        where TState : struct, Enum
    {
        /// <summary>Executes a synchronous action when the state machine transition is triggered.</summary>
        public IStateMachineBuilderWhenClause<TState, TNotification> Execute(Action<TNotification, IServiceProvider> action)
        {
            return clause.ExecuteAsync((notification, sp, _) =>
            {
                action(notification, sp);
                return Task.CompletedTask;
            });
        }

        /// <summary>Sends a command asynchronously when the transition is triggered.</summary>
        public IStateMachineBuilderWhenClause<TState, TNotification> SendCommandAsync<TCommand>(Func<TNotification, TCommand> commandFactory)
        {
            return clause.ExecuteAsync(async (notification, sp, cancellationToken) =>
            {
                var commandSender = sp.GetRequiredService<ICommandSender>();
                var contextAccessor = sp.GetRequiredService<IContextAccessor>();
                await commandSender.SendAsync(commandFactory(notification), contextAccessor.Context, cancellationToken: cancellationToken).ConfigureAwait(false);
            });
        }

        /// <summary>Publishes a notification asynchronously when the transition is triggered.</summary>
        public IStateMachineBuilderWhenClause<TState, TNotification> PublishNotificationAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TAnotherNotification>(Func<TNotification, TAnotherNotification> notificationFactory)
        {
            return clause.ExecuteAsync(async (notification, sp, cancellationToken) =>
            {
                var notificationPublisher = sp.GetRequiredService<INotificationPublisher>();
                var contextAccessor = sp.GetRequiredService<IContextAccessor>();
                await notificationPublisher.PublishAsync(notificationFactory(notification), contextAccessor.Context, cancellationToken: cancellationToken).ConfigureAwait(false);
            });
        }
    }
}

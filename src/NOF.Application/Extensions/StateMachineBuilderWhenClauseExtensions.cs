using Microsoft.Extensions.DependencyInjection;

namespace NOF;

/// <summary>
/// Extension methods for the NOF.Application layer.
/// </summary>
public static partial class __NOF_Application_Extensions__
{
    extension<TState, TContext, TNotification>(IStateMachineBuilderWhenClause<TState, TContext, TNotification> when)
        where TContext : class, IStateMachineContext
        where TNotification : class, INotification
        where TState : struct, Enum
    {
        /// <summary>Executes a synchronous action when the state machine transition is triggered.</summary>
        /// <param name="action">The action to execute with context, notification, and service provider.</param>
        /// <returns>The when clause for further chaining.</returns>
        public IStateMachineBuilderWhenClause<TState, TContext, TNotification> Execute(Action<TContext, TNotification, IServiceProvider> action)
        {
            return when.ExecuteAsync((context, notification, sp, _) =>
            {
                action(context, notification, sp);
                return Task.CompletedTask;
            });
        }

        /// <summary>Modifies the state machine context when the transition is triggered.</summary>
        /// <param name="action">The action to modify the context.</param>
        /// <returns>The when clause for further chaining.</returns>
        public IStateMachineBuilderWhenClause<TState, TContext, TNotification> Modify(Action<TContext, TNotification> action)
        {
            return when.Execute((context, notification, sp) =>
            {
                action(context, notification);
            });
        }

        /// <summary>Sends a command asynchronously when the transition is triggered.</summary>
        /// <typeparam name="TCommand">The command type.</typeparam>
        /// <param name="commandFactory">Factory to create the command from context and notification.</param>
        /// <returns>The when clause for further chaining.</returns>
        public IStateMachineBuilderWhenClause<TState, TContext, TNotification> SendCommandAsync<TCommand>(Func<TContext, TNotification, TCommand> commandFactory)
            where TCommand : class, ICommand
        {
            return when.ExecuteAsync(async (context, notification, sp, cancellationToken) =>
            {
                var commandSender = sp.GetRequiredService<ICommandSender>();
                await commandSender.SendAsync(commandFactory(context, notification), cancellationToken: cancellationToken);
            });
        }

        /// <summary>Publishes a notification asynchronously when the transition is triggered.</summary>
        /// <typeparam name="TAnotherNotification">The notification type to publish.</typeparam>
        /// <param name="notificationFactory">Factory to create the notification from context and notification.</param>
        /// <returns>The when clause for further chaining.</returns>
        public IStateMachineBuilderWhenClause<TState, TContext, TNotification> PublishNotificationAsync<TAnotherNotification>(Func<TContext, TNotification, TAnotherNotification> notificationFactory)
            where TAnotherNotification : class, INotification
        {
            return when.ExecuteAsync(async (context, notification, sp, cancellationToken) =>
            {
                var notificationPublisher = sp.GetRequiredService<INotificationPublisher>();
                await notificationPublisher.PublishAsync(notificationFactory(context, notification), cancellationToken);
            });
        }
    }
}

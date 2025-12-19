using Microsoft.Extensions.DependencyInjection;
using NOF.Application.Internals;

namespace NOF;

public static partial class __NOF_Application_Extensions__
{
    extension<TState, TContext, TNotification>(IStateMachineBuilderWhenClause<TState, TContext, TNotification> when)
        where TContext : class, IStateMachineContext<TState>, new()
        where TNotification : class, INotification
        where TState : struct, Enum
    {
        public IStateMachineBuilderWhenClause<TState, TContext, TNotification> Execute(Action<TContext, TNotification, IServiceProvider> action)
        {
            return when.ExecuteAsync((context, notification, sp, _) =>
            {
                action(context, notification, sp);
                return Task.CompletedTask;
            });
        }

        public IStateMachineBuilderWhenClause<TState, TContext, TNotification> Modify(Action<TContext, TNotification> action)
        {
            return when.Execute((context, notification, sp) =>
            {
                action(context, notification);
            });
        }

        public IStateMachineBuilderWhenClause<TState, TContext, TNotification> SendCommandAsync<TCommand>(Func<TContext, TNotification, TCommand> commandFactory)
            where TCommand : class, ICommand
        {
            return when.ExecuteAsync(async (context, notification, sp, cancellationToken) =>
            {
                var commandSender = sp.GetRequiredService<ICommandSender>();
                await commandSender.SendAsync(commandFactory(context, notification), cancellationToken: cancellationToken);
            });
        }

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

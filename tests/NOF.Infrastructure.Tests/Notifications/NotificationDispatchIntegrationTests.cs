using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using Xunit;

namespace NOF.Infrastructure.Tests.Notifications;

public sealed class NotificationDispatchIntegrationTests
{
    [Fact]
    public async Task PublishAsync_ShouldDispatchConcreteNotificationToEveryConcreteHandler_EndToEnd()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddNOFInfrastructure();
        builder.Services.Configure<TransactionalMessageOptions>(static options =>
        {
            options.Inbox.PollingInterval = TimeSpan.FromMilliseconds(10);
            options.Inbox.BatchSize = 10;
            options.Outbox.PollingInterval = TimeSpan.FromMilliseconds(10);
        });

        var registry = builder.Services.GetOrAddSingleton<NotificationHandlerRegistry>();
        registry.Add(new NotificationHandlerRegistration(
            typeof(FirstConcreteNotificationHandler),
            typeof(ConcreteNotification),
            typeof(NotificationInboundInvoker<FirstConcreteNotificationHandler, ConcreteNotification>)));
        registry.Add(new NotificationHandlerRegistration(
            typeof(SecondConcreteNotificationHandler),
            typeof(ConcreteNotification),
            typeof(NotificationInboundInvoker<SecondConcreteNotificationHandler, ConcreteNotification>)));
        registry.Add(new NotificationHandlerRegistration(
            typeof(BaseNotificationHandler),
            typeof(BaseNotification),
            typeof(NotificationInboundInvoker<BaseNotificationHandler, BaseNotification>)));

        builder.Services.AddSingleton<NotificationDispatchProbe>();
        builder.Services.AddScoped<FirstConcreteNotificationHandler>();
        builder.Services.AddScoped<SecondConcreteNotificationHandler>();
        builder.Services.AddScoped<BaseNotificationHandler>();
        builder.Services.AddSingleton<NotificationInboundInvoker<FirstConcreteNotificationHandler, ConcreteNotification>>();
        builder.Services.AddSingleton<NotificationInboundInvoker<SecondConcreteNotificationHandler, ConcreteNotification>>();
        builder.Services.AddSingleton<NotificationInboundInvoker<BaseNotificationHandler, BaseNotification>>();

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(timeout.Token);

        try
        {
            using (var scope = host.Services.CreateScope())
            {
                var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
                await publisher.PublishAsync(new ConcreteNotification("published"), Context.Empty, timeout.Token);
            }

            var probe = host.Services.GetRequiredService<NotificationDispatchProbe>();
            await WaitUntilAsync(() => probe.FirstConcreteCount == 1 && probe.SecondConcreteCount == 1, timeout.Token);

            using var verificationScope = host.Services.CreateScope();
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<IDbContext>();
            var inboxMessages = await dbContext.Set<NOFInboxMessage>()
                .OrderBy(static message => message.Route)
                .ToListAsync(timeout.Token);

            Assert.Equal(2, inboxMessages.Count);
            Assert.Single(inboxMessages.Select(static message => message.Id).Distinct());
            Assert.All(inboxMessages, static message => Assert.Equal(InboxMessageStatus.Processed, message.Status));
            Assert.Contains(inboxMessages, static message => message.Route == typeof(FirstConcreteNotificationHandler).DisplayName);
            Assert.Contains(inboxMessages, static message => message.Route == typeof(SecondConcreteNotificationHandler).DisplayName);
            Assert.DoesNotContain(inboxMessages, static message => message.Route == typeof(BaseNotificationHandler).DisplayName);
            Assert.Equal(0, probe.BaseCount);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
    }

    private abstract record BaseNotification(string Value);

    private sealed record ConcreteNotification(string Value) : BaseNotification(Value);

    private sealed class NotificationDispatchProbe
    {
        private int _firstConcreteCount;
        private int _secondConcreteCount;
        private int _baseCount;

        public int FirstConcreteCount => Volatile.Read(ref _firstConcreteCount);

        public int SecondConcreteCount => Volatile.Read(ref _secondConcreteCount);

        public int BaseCount => Volatile.Read(ref _baseCount);

        public void MarkFirstConcrete() => Interlocked.Increment(ref _firstConcreteCount);

        public void MarkSecondConcrete() => Interlocked.Increment(ref _secondConcreteCount);

        public void MarkBase() => Interlocked.Increment(ref _baseCount);
    }

    private sealed class FirstConcreteNotificationHandler(NotificationDispatchProbe probe) : NotificationHandler<ConcreteNotification>
    {
        public override Task HandleAsync(ConcreteNotification notification, Context context, CancellationToken cancellationToken)
        {
            probe.MarkFirstConcrete();
            return Task.CompletedTask;
        }
    }

    private sealed class SecondConcreteNotificationHandler(NotificationDispatchProbe probe) : NotificationHandler<ConcreteNotification>
    {
        public override Task HandleAsync(ConcreteNotification notification, Context context, CancellationToken cancellationToken)
        {
            probe.MarkSecondConcrete();
            return Task.CompletedTask;
        }
    }

    private sealed class BaseNotificationHandler(NotificationDispatchProbe probe) : NotificationHandler<BaseNotification>
    {
        public override Task HandleAsync(BaseNotification notification, Context context, CancellationToken cancellationToken)
        {
            probe.MarkBase();
            return Task.CompletedTask;
        }
    }

    private sealed class NotificationInboundInvoker<THandler, TNotification> : INotificationInboundHandlerInvoker
        where THandler : NotificationHandler<TNotification>
    {
        public string HandlerTypeName => typeof(THandler).DisplayName;

        public Type HandlerType => typeof(THandler);

        public string MessageTypeName => typeof(TNotification).DisplayName;

        public Type MessageType => typeof(TNotification);

        public object Bind(
            ReadOnlyMemory<byte> payload,
            Func<ReadOnlyMemory<byte>, Type, object?> deserialize)
        {
            ArgumentNullException.ThrowIfNull(deserialize);
            return deserialize(payload, typeof(TNotification))
                ?? throw new InvalidOperationException($"Failed to deserialize message payload as '{typeof(TNotification).DisplayName}'.");
        }

        public ValueTask InvokeAsync(
            IServiceProvider services,
            object message,
            Context context,
            CancellationToken cancellationToken)
        {
            var handler = services.GetRequiredService<THandler>();
            return new ValueTask(handler.HandleAsync((TNotification)message, context, cancellationToken));
        }
    }
}

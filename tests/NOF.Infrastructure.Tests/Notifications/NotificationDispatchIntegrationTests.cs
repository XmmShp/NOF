using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Collections.Concurrent;
using Xunit;

namespace NOF.Infrastructure.Tests.Notifications;

public sealed class NotificationDispatchIntegrationTests
{
    [Fact]
    public async Task DeferPublishOrderedAsync_ShouldQualifyKeyAndAllocateContiguousSequences()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddNOFInfrastructure();

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        await publisher.DeferPublishOrderedAsync(
            new ConcreteNotification("first"),
            "Invoice:42",
            Context.Empty);
        await publisher.DeferPublishOrderedAsync(
            new ConcreteNotification("second"),
            "Invoice:42",
            Context.Empty,
            completesOrderKey: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.DeferPublishOrderedAsync(
            new ConcreteNotification("after-terminal"),
            "Invoice:42",
            Context.Empty));
        await publisher.DeferPublishAsync(new ConcreteNotification("unordered"), Context.Empty);
        await dbContext.SaveChangesAsync();

        var messages = await dbContext.Set<NOFOutboxMessage>()
            .Where(static message => message.OrderKey != null)
            .OrderBy(static message => message.Sequence)
            .ToListAsync();
        var allocations = await dbContext.Set<NOFOutboxOrderState>()
            .OrderBy(static state => state.Sequence)
            .ToListAsync();

        Assert.Equal(2, messages.Count);
        Assert.Equal([1L, 2L], messages.Select(static message => message.Sequence!.Value).ToArray());
        Assert.All(messages, message => Assert.Equal($"{environment.ServiceName}:Invoice:42", message.OrderKey));
        Assert.False(messages[0].CompletesOrderKey);
        Assert.True(messages[1].CompletesOrderKey);
        Assert.Equal([1L, 2L], allocations.Select(static state => state.Sequence).ToArray());

        var unordered = await dbContext.Set<NOFOutboxMessage>()
            .SingleAsync(static message => message.OrderKey == null);
        Assert.Null(unordered.Sequence);
        Assert.False(unordered.CompletesOrderKey);

        using var secondScope = host.Services.CreateScope();
        var secondPublisher = secondScope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => secondPublisher.DeferPublishOrderedAsync(
            new ConcreteNotification("after-persisted-terminal"),
            "Invoice:42",
            Context.Empty));
    }

    [Fact]
    public async Task ConcurrentOrderedAllocations_ShouldConflictAndRetryWithNextSequence()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddNOFInfrastructure();

        using var host = builder.Build();
        using var firstScope = host.Services.CreateScope();
        using var secondScope = host.Services.CreateScope();
        var firstPublisher = firstScope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var secondPublisher = secondScope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<IDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<IDbContext>();

        await firstPublisher.DeferPublishOrderedAsync(
            new ConcreteNotification("first-instance"),
            "Invoice:99",
            Context.Empty);
        await secondPublisher.DeferPublishOrderedAsync(
            new ConcreteNotification("second-instance"),
            "Invoice:99",
            Context.Empty);

        await firstDbContext.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => secondDbContext.SaveChangesAsync());

        using var retryScope = host.Services.CreateScope();
        var retryPublisher = retryScope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var retryDbContext = retryScope.ServiceProvider.GetRequiredService<IDbContext>();
        await retryPublisher.DeferPublishOrderedAsync(
            new ConcreteNotification("retry"),
            "Invoice:99",
            Context.Empty);
        await retryDbContext.SaveChangesAsync();

        var sequences = await retryDbContext.Set<NOFOutboxOrderState>()
            .Where(static state => state.OrderKey.EndsWith(":Invoice:99"))
            .OrderBy(static state => state.Sequence)
            .Select(static state => state.Sequence)
            .ToListAsync();
        Assert.Equal([1L, 2L], sequences);
    }

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

    [Fact]
    public async Task PublishAsync_WhenHandlerThrows_ShouldRetryAndMarkInboxAsFailed()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddNOFInfrastructure();
        builder.Services.Configure<TransactionalMessageOptions>(static options =>
        {
            options.Inbox.PollingInterval = TimeSpan.FromMilliseconds(10);
            options.Inbox.BatchSize = 10;
            options.Inbox.MaxRetryCount = 2;
        });

        var registry = builder.Services.GetOrAddSingleton<NotificationHandlerRegistry>();
        registry.Add(new NotificationHandlerRegistration(
            typeof(FailingNotificationHandler),
            typeof(FailingNotification),
            typeof(NotificationInboundInvoker<FailingNotificationHandler, FailingNotification>)));
        builder.Services.AddSingleton<FailingNotificationProbe>();
        builder.Services.AddScoped<FailingNotificationHandler>();
        builder.Services.AddSingleton<NotificationInboundInvoker<FailingNotificationHandler, FailingNotification>>();

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(timeout.Token);

        try
        {
            using (var scope = host.Services.CreateScope())
            {
                var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
                await publisher.PublishAsync(new FailingNotification(), Context.Empty, timeout.Token);
            }

            var probe = host.Services.GetRequiredService<FailingNotificationProbe>();
            await WaitUntilAsync(() => probe.AttemptCount == 2, timeout.Token);
            var inboxMessage = await WaitForInboxStatusAsync(
                host.Services,
                InboxMessageStatus.Failed,
                timeout.Token);

            Assert.Equal(2, inboxMessage.RetryCount);
            Assert.Null(inboxMessage.ProcessedAtUtc);
            Assert.NotNull(inboxMessage.FailedAtUtc);
            Assert.Equal("Notification failed.", inboxMessage.ErrorMessage);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task DeferPublishAsync_WhenDeliveryFails_ShouldUseAllRetriesAndReportFailedBatch()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddNOFInfrastructure();
        builder.Services.Configure<TransactionalMessageOptions>(static options =>
        {
            options.Outbox.PollingInterval = TimeSpan.FromMilliseconds(10);
            options.Outbox.BatchSize = 10;
            options.Outbox.MaxRetryCount = 2;
        });

        var rider = new AlwaysFailingNotificationRider();
        builder.Services.ReplaceOrAddSingleton<INotificationRider>(rider);
        var loggerProvider = new CapturingLoggerProvider();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(loggerProvider);

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(timeout.Token);

        try
        {
            using (var scope = host.Services.CreateScope())
            {
                var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
                var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
                await publisher.DeferPublishAsync(new FailingNotification(), Context.Empty, timeout.Token);
                await dbContext.SaveChangesAsync(timeout.Token);
            }

            await WaitUntilAsync(() => rider.AttemptCount == 2, timeout.Token);
            var outboxMessage = await WaitForOutboxStatusAsync(
                host.Services,
                OutboxMessageStatus.Failed,
                timeout.Token);
            await WaitUntilAsync(
                () => loggerProvider.Messages.Contains("Outbox batch processed: 0 sent, 1 failed"),
                timeout.Token);

            Assert.Equal(2, outboxMessage.RetryCount);
            Assert.Null(outboxMessage.SentAtUtc);
            Assert.NotNull(outboxMessage.FailedAtUtc);
            Assert.Equal("Transport unavailable.", outboxMessage.ErrorMessage);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OrderedNotification_ShouldWaitForMissingPredecessorAndThenProcessContiguously()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddNOFInfrastructure();
        builder.Services.Configure<TransactionalMessageOptions>(static options =>
        {
            options.Inbox.PollingInterval = TimeSpan.FromMilliseconds(10);
            options.Inbox.BatchSize = 10;
        });

        var registry = builder.Services.GetOrAddSingleton<NotificationHandlerRegistry>();
        registry.Add(new NotificationHandlerRegistration(
            typeof(OrderedNotificationHandler),
            typeof(OrderedNotification),
            typeof(NotificationInboundInvoker<OrderedNotificationHandler, OrderedNotification>)));
        builder.Services.AddSingleton<OrderedNotificationProbe>();
        builder.Services.AddScoped<OrderedNotificationHandler>();
        builder.Services.AddSingleton<NotificationInboundInvoker<OrderedNotificationHandler, OrderedNotification>>();

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(timeout.Token);

        try
        {
            var rider = host.Services.GetRequiredService<INotificationRider>();
            var serializer = host.Services.GetRequiredService<IObjectSerializer>();
            const string orderKey = "ProducerService:Invoice:42";

            await PublishOrderedAsync(rider, serializer, new OrderedNotification(2), orderKey, 2, true, timeout.Token);
            await Task.Delay(100, timeout.Token);
            Assert.Empty(host.Services.GetRequiredService<OrderedNotificationProbe>().Values);

            await PublishOrderedAsync(rider, serializer, new OrderedNotification(1), orderKey, 1, false, timeout.Token);
            var probe = host.Services.GetRequiredService<OrderedNotificationProbe>();
            await WaitUntilAsync(() => probe.Values.Count == 2, timeout.Token);

            Assert.Equal([1, 2], probe.Values);

            using var verificationScope = host.Services.CreateScope();
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<IDbContext>();
            var state = await dbContext.Set<NOFInboxOrderState>().SingleAsync(timeout.Token);
            Assert.Equal(3, state.NextSequence);
            Assert.NotNull(state.CompletedAtUtc);
            Assert.Null(state.BlockedSequence);

            await PublishOrderedAsync(rider, serializer, new OrderedNotification(3), orderKey, 1, false, timeout.Token);
            await WaitUntilAsync(() => probe.Values.Count == 3, timeout.Token);
            Assert.Equal([1, 2, 3], probe.Values);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static Task PublishOrderedAsync(
        INotificationRider rider,
        IObjectSerializer serializer,
        OrderedNotification notification,
        string orderKey,
        long sequence,
        bool completesOrderKey,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string?>
        {
            [NOFAbstractionConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString(),
            [NOFAbstractionConstants.Transport.Headers.OrderKey] = orderKey,
            [NOFAbstractionConstants.Transport.Headers.Sequence] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [NOFAbstractionConstants.Transport.Headers.CompletesOrderKey] = completesOrderKey.ToString()
        };
        return rider.PublishAsync(
            serializer.Serialize(notification),
            typeof(OrderedNotification).DisplayName,
            headers,
            cancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
    }

    private static async Task<NOFInboxMessage> WaitForInboxStatusAsync(
        IServiceProvider services,
        InboxMessageStatus status,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
            var message = await dbContext.Set<NOFInboxMessage>()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (message?.Status == status)
            {
                return message;
            }

            await Task.Delay(10, cancellationToken);
        }
    }

    private static async Task<NOFOutboxMessage> WaitForOutboxStatusAsync(
        IServiceProvider services,
        OutboxMessageStatus status,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
            var message = await dbContext.Set<NOFOutboxMessage>()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (message?.Status == status)
            {
                return message;
            }

            await Task.Delay(10, cancellationToken);
        }
    }

    private abstract record BaseNotification(string Value);

    private sealed record ConcreteNotification(string Value) : BaseNotification(Value);

    private sealed record OrderedNotification(int Value);

    private sealed record FailingNotification;

    private sealed class AlwaysFailingNotificationRider : INotificationRider
    {
        private int _attemptCount;

        public int AttemptCount => Volatile.Read(ref _attemptCount);

        public Task PublishAsync(
            ReadOnlyMemory<byte> payload,
            string messageRoute,
            IEnumerable<KeyValuePair<string, string?>>? headers,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _attemptCount);
            throw new InvalidOperationException("Transport unavailable.");
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
            }
        }
    }

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

    private sealed class OrderedNotificationProbe
    {
        private readonly object _sync = new();
        private readonly List<int> _values = [];

        public IReadOnlyList<int> Values
        {
            get
            {
                lock (_sync)
                {
                    return [.. _values];
                }
            }
        }

        public void Add(int value)
        {
            lock (_sync)
            {
                _values.Add(value);
            }
        }
    }

    private sealed class OrderedNotificationHandler(OrderedNotificationProbe probe) : NotificationHandler<OrderedNotification>
    {
        public override Task HandleAsync(OrderedNotification notification, Context context, CancellationToken cancellationToken)
        {
            probe.Add(notification.Value);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingNotificationProbe
    {
        private int _attemptCount;

        public int AttemptCount => Volatile.Read(ref _attemptCount);

        public void MarkAttempt() => Interlocked.Increment(ref _attemptCount);
    }

    private sealed class FailingNotificationHandler(FailingNotificationProbe probe) : NotificationHandler<FailingNotification>
    {
        public override Task HandleAsync(FailingNotification notification, Context context, CancellationToken cancellationToken)
        {
            probe.MarkAttempt();
            throw new InvalidOperationException("Notification failed.");
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

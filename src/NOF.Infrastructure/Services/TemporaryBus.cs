using MassTransit;

namespace NOF;

public interface ITemporaryBus : IBus, IDisposable;

internal class TemporaryBus : ITemporaryBus
{
    private readonly IBusControl _bus;
    internal TemporaryBus(IBusControl bus)
    {
        _bus = bus;
        _bus.Start();
    }
    public void Dispose()
    {
        _bus.Stop();
    }

    #region Forward Method Wrapper

    public ConnectHandle ConnectPublishObserver(IPublishObserver observer)
        => _bus.ConnectPublishObserver(observer);

    public Task<ISendEndpoint> GetPublishSendEndpoint<T>() where T : class
        => _bus.GetPublishSendEndpoint<T>();

    public Task Publish<T>(T message, CancellationToken cancellationToken = new CancellationToken()) where T : class
        => _bus.Publish(message, cancellationToken);

    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = new CancellationToken()) where T : class
        => _bus.Publish(message, publishPipe, cancellationToken);

    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = new CancellationToken()) where T : class
        => _bus.Publish(message, publishPipe, cancellationToken);

    public Task Publish(object message, CancellationToken cancellationToken = new CancellationToken())
        => _bus.Publish(message, cancellationToken);

    public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = new CancellationToken())
        => _bus.Publish(message, publishPipe, cancellationToken);

    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = new CancellationToken())
        => _bus.Publish(message, messageType, cancellationToken);

    public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe,
        CancellationToken cancellationToken = new CancellationToken())
        => _bus.Publish(message, messageType, publishPipe, cancellationToken);

    public Task Publish<T>(object values, CancellationToken cancellationToken = new CancellationToken()) where T : class
        => _bus.Publish(values, cancellationToken);

    public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = new CancellationToken()) where T : class
        => _bus.Publish(values, publishPipe, cancellationToken);

    public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = new CancellationToken()) where T : class
        => _bus.Publish(values, publishPipe, cancellationToken);

    public ConnectHandle ConnectSendObserver(ISendObserver observer)
        => _bus.ConnectSendObserver(observer);

    public Task<ISendEndpoint> GetSendEndpoint(Uri address)
        => _bus.GetSendEndpoint(address);

    public ConnectHandle ConnectConsumePipe<T>(IPipe<ConsumeContext<T>> pipe) where T : class
        => _bus.ConnectConsumePipe(pipe);

    public ConnectHandle ConnectConsumePipe<T>(IPipe<ConsumeContext<T>> pipe, ConnectPipeOptions options) where T : class
        => _bus.ConnectConsumePipe(pipe, options);

    public ConnectHandle ConnectRequestPipe<T>(Guid requestId, IPipe<ConsumeContext<T>> pipe) where T : class
        => _bus.ConnectRequestPipe(requestId, pipe);

    public ConnectHandle ConnectConsumeMessageObserver<T>(IConsumeMessageObserver<T> observer) where T : class
        => _bus.ConnectConsumeMessageObserver(observer);

    public ConnectHandle ConnectConsumeObserver(IConsumeObserver observer)
        => _bus.ConnectConsumeObserver(observer);

    public ConnectHandle ConnectReceiveObserver(IReceiveObserver observer)
        => _bus.ConnectReceiveObserver(observer);

    public ConnectHandle ConnectReceiveEndpointObserver(IReceiveEndpointObserver observer)
        => _bus.ConnectReceiveEndpointObserver(observer);

    public ConnectHandle ConnectEndpointConfigurationObserver(IEndpointConfigurationObserver observer)
        => _bus.ConnectEndpointConfigurationObserver(observer);

    public HostReceiveEndpointHandle ConnectReceiveEndpoint(IEndpointDefinition definition,
        IEndpointNameFormatter? endpointNameFormatter = null, Action<IReceiveEndpointConfigurator>? configureEndpoint = null)
        => _bus.ConnectReceiveEndpoint(definition, endpointNameFormatter, configureEndpoint);

    public HostReceiveEndpointHandle ConnectReceiveEndpoint(string queueName, Action<IReceiveEndpointConfigurator>? configureEndpoint = null)
        => _bus.ConnectReceiveEndpoint(queueName, configureEndpoint);

    public void Probe(ProbeContext context)
        => _bus.Probe(context);

    public Uri Address => _bus.Address;
    public IBusTopology Topology => _bus.Topology;
    public Task<BusHandle> StartAsync(CancellationToken cancellationToken = new CancellationToken())
        => _bus.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = new CancellationToken())
        => _bus.StopAsync(cancellationToken);

    public BusHealthResult CheckHealth()
        => _bus.CheckHealth();

    #endregion
}

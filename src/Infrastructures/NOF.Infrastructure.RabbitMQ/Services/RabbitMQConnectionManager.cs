using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQConnectionManager : IDisposable, IAsyncDisposable
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly RabbitMQOptions _options;
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;

    public RabbitMQConnectionManager(IOptions<RabbitMQOptions> options)
    {
        _options = options.Value;
        _connectionFactory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };
    }

    internal async Task<IConnection> GetConnectionAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMQConnectionManager));
        }

        if (_connection != null && _connection.IsOpen)
        {
            return _connection;
        }

        await _connectionSemaphore.WaitAsync();
        try
        {
            if (_connection != null && _connection.IsOpen)
            {
                return _connection;
            }

            _connection?.Dispose();
            _connection = await _connectionFactory.CreateConnectionAsync();
            return _connection;
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    internal async Task<IChannel> CreateChannelAsync()
    {
        var connection = await GetConnectionAsync();
        return await connection.CreateChannelAsync();
    }

    internal async Task<IChannel> CreatePublisherChannelAsync()
    {
        var connection = await GetConnectionAsync();
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: _options.PublisherConfirmationsEnabled,
            publisherConfirmationTrackingEnabled: _options.PublisherConfirmationTrackingEnabled);
        return await connection.CreateChannelAsync(options);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connectionSemaphore.Wait();
        try
        {
            if (_disposed)
            {
                return;
            }

            _connection?.Dispose();
            _connection = null;
            _disposed = true;
        }
        finally
        {
            _connectionSemaphore.Release();
            _connectionSemaphore.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _connectionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _connection?.Dispose();
            _connection = null;
            _disposed = true;
        }
        finally
        {
            _connectionSemaphore.Release();
            _connectionSemaphore.Dispose();
        }
    }
}

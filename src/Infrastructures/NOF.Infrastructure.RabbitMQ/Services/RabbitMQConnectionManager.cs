using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQConnectionManager : IDisposable
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;

    public RabbitMQConnectionManager(IOptions<RabbitMQOptions> options)
    {
        _connectionFactory = new ConnectionFactory
        {
            HostName = options.Value.HostName,
            Port = options.Value.Port,
            UserName = options.Value.UserName,
            Password = options.Value.Password,
            VirtualHost = options.Value.VirtualHost
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
}

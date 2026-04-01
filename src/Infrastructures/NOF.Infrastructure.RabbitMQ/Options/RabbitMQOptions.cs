namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQOptions
{
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; } = 0;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public bool Durable { get; set; } = true;
    public bool AutoDelete { get; set; } = false;
    public ushort PrefetchCount { get; set; } = 1;
}

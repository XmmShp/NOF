namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQOptions
{
    public string? ConnectionString
    {
        get
        {
            if (string.IsNullOrEmpty(HostName))
            {
                return null;
            }

            var parts = new List<string>();

            if (!string.IsNullOrEmpty(HostName))
            {
                parts.Add($"Host={HostName}");
            }

            if (Port > 0)
            {
                parts.Add($"Port={Port}");
            }

            if (!string.IsNullOrEmpty(UserName))
            {
                parts.Add($"UserName={UserName}");
            }

            if (!string.IsNullOrEmpty(Password))
            {
                parts.Add($"Password={Password}");
            }

            if (!string.IsNullOrEmpty(VirtualHost))
            {
                parts.Add($"VirtualHost={VirtualHost}");
            }

            return string.Join(";", parts);
        }
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                ParseConnectionString(value);
            }
        }
    }

    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; } = 0;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public bool Durable { get; set; } = true;
    public bool AutoDelete { get; set; } = false;
    public ushort PrefetchCount { get; set; } = 1;

    private void ParseConnectionString(string connectionString)
    {
        if (connectionString.StartsWith("amqp://", StringComparison.OrdinalIgnoreCase))
        {
            ParseAmqpConnectionString(connectionString);
        }
        else
        {
            ParseKeyValueConnectionString(connectionString);
        }
    }

    private void ParseAmqpConnectionString(string connectionString)
    {
        var uri = new Uri(connectionString);
        HostName = uri.Host;
        Port = uri.Port > 0 ? uri.Port : 0;
        UserName = uri.UserInfo.Split(':').FirstOrDefault() ?? string.Empty;
        Password = uri.UserInfo.Split(':').Skip(1).FirstOrDefault() ?? string.Empty;
        VirtualHost = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
                      ? "/"
                      : uri.AbsolutePath.TrimStart('/');
    }

    private void ParseKeyValueConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (keyValue.Length == 2)
            {
                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                switch (key.ToLowerInvariant())
                {
                    case "host":
                    case "hostname":
                        HostName = value;
                        break;
                    case "port":
                        if (int.TryParse(value, out var port))
                        {
                            Port = port;
                        }
                        break;
                    case "username":
                    case "user":
                    case "uid":
                        UserName = value;
                        break;
                    case "password":
                    case "pwd":
                        Password = value;
                        break;
                    case "virtualhost":
                    case "vhost":
                    case "path":
                        VirtualHost = string.IsNullOrEmpty(value) ? "/" : value;
                        break;
                }
            }
        }
    }
}

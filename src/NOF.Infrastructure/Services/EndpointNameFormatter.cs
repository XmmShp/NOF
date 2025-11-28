using MassTransit;
using System.Reflection;

namespace NOF;

public class EndpointNameFormatter : KebabCaseEndpointNameFormatter
{
    public new static EndpointNameFormatter Instance { get; } = new();

    public override string Consumer<T>()
    {
        if (typeof(T).GetCustomAttribute<ConsumerQueueAttribute>() is { } attribute)
        {
            return attribute.QueueName;
        }

        return base.Consumer<T>();
    }

    public string GetMessageName(object obj)
        => GetMessageName(type: obj.GetType());

    protected override string GetMessageName(Type type)
    {
        var messageName = base.GetMessageName(type);
        const string command = "-command";
        if (messageName.EndsWith(command))
        {
            messageName = messageName[..^command.Length];
        }
        return messageName;
    }
}

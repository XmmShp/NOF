namespace NOF;

[AttributeUsage(AttributeTargets.Class)]
public class ConsumerQueueAttribute(string queueName) : Attribute
{
    public string QueueName { get; } = queueName;
}

namespace NOF.Infrastructure.RabbitMQ;
/// <summary>Reports a failure while processing a RabbitMQ message.</summary>

public sealed class RabbitMQConsumerMessageException : Exception
{
    public RabbitMQConsumerMessageException(string message, bool requeue, Exception? innerException = null)
        : base(message, innerException)
    {
        Requeue = requeue;
    }

    public bool Requeue { get; }
}

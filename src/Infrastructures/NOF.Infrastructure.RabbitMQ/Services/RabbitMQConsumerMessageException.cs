namespace NOF.Infrastructure.RabbitMQ;

internal sealed class RabbitMQConsumerMessageException : Exception
{
    public RabbitMQConsumerMessageException(string message, bool requeue, Exception? innerException = null)
        : base(message, innerException)
    {
        Requeue = requeue;
    }

    public bool Requeue { get; }
}

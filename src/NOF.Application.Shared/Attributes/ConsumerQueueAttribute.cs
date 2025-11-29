using System;

namespace NOF;

/// <summary>
/// This attribute works in conjunction with EndpointNameFormatter to specify the queue
/// on which the annotated consumer listens for messages.
/// Although this functionality is conceptually part of the Infrastructure layer,
/// it is defined in the Application layer for convenience and ease of use.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ConsumerQueueAttribute(string queueName) : Attribute
{
    public string QueueName { get; } = queueName;
}

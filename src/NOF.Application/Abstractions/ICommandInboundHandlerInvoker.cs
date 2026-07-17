using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public interface ICommandInboundHandlerInvoker
{
    string HandlerTypeName { get; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    Type HandlerType { get; }

    string MessageTypeName { get; }

    Type MessageType { get; }

    object Bind(
        ReadOnlyMemory<byte> payload,
        Func<ReadOnlyMemory<byte>, Type, object?> deserialize);

    ValueTask InvokeAsync(
        IServiceProvider services,
        object message,
        Context context,
        CancellationToken cancellationToken);
}

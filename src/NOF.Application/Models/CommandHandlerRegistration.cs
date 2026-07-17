using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public record CommandHandlerRegistration(
    [property: DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    [param: DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    Type HandlerType,
    Type CommandType,
    [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type? Invoker = null)
{
    public string HandlerTypeName { get; } = HandlerType.DisplayName;

    public string CommandTypeName { get; } = CommandType.DisplayName;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type InvokerType { get; } = Invoker ?? HandlerType;
}

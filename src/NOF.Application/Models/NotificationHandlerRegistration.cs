using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public record NotificationHandlerRegistration(
    [property: DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    [param: DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    Type HandlerType,
    Type NotificationType,
    [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type? Invoker = null)
{
    public string HandlerTypeName { get; } = HandlerType.DisplayName;

    public string NotificationTypeName { get; } = NotificationType.DisplayName;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type InvokerType { get; } = Invoker ?? HandlerType;
}

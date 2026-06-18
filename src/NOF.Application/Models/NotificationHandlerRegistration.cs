using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public record NotificationHandlerRegistration(
    [property: DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    Type HandlerType,
    Type NotificationType);

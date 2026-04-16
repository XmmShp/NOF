using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public record NotificationHandlerRegistration(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    Type NotificationType);

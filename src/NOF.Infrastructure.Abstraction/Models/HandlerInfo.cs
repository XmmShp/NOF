using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure.Abstraction;

public record CommandHandlerInfo(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    Type CommandType);

public record EventHandlerInfo(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    Type EventType);

public record NotificationHandlerInfo(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    Type NotificationType);

public record RequestWithoutResponseHandlerInfo(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    Type RequestType);

public record RequestWithResponseHandlerInfo(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    Type RequestType,
    Type ResponseType);

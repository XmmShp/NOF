using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure.Abstraction;

public abstract record HandlerInfo(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType);

public record CommandHandlerInfo(
    Type HandlerType,
    Type CommandType) : HandlerInfo(HandlerType);

public record EventHandlerInfo(
    Type HandlerType,
    Type EventType) : HandlerInfo(HandlerType);

public record NotificationHandlerInfo(
    Type HandlerType,
    Type NotificationType) : HandlerInfo(HandlerType);

public record RequestWithoutResponseHandlerInfo(
    Type HandlerType,
    Type RequestType) : HandlerInfo(HandlerType);

public record RequestWithResponseHandlerInfo(
    Type HandlerType,
    Type RequestType,
    Type ResponseType) : HandlerInfo(HandlerType);

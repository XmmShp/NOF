using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

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

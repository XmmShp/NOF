namespace NOF.Infrastructure.Abstraction;

public record CommandHandlerInfo(Type HandlerType, Type CommandType);

public record EventHandlerInfo(Type HandlerType, Type EventType);

public record NotificationHandlerInfo(Type HandlerType, Type NotificationType);

public record RequestWithoutResponseHandlerInfo(Type HandlerType, Type RequestType);

public record RequestWithResponseHandlerInfo(Type HandlerType, Type RequestType, Type ResponseType);

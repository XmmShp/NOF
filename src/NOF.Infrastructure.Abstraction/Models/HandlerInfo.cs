namespace NOF.Infrastructure.Abstraction;

public enum HandlerKind
{
    Command,
    Event,
    Notification,
    RequestWithoutResponse,
    RequestWithResponse
}

public record HandlerInfo(HandlerKind Kind, Type HandlerType, Type MessageType, Type? ResponseType);

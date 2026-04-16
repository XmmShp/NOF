using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public record CommandHandlerRegistration(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    Type CommandType);

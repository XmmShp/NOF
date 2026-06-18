using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public record CommandHandlerRegistration(
    [property: DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    Type HandlerType,
    Type CommandType);

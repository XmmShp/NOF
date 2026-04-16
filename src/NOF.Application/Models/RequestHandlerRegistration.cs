namespace NOF.Application;

public sealed record RequestHandlerRegistration(
    Type ServiceType,
    Type ImplementationType);

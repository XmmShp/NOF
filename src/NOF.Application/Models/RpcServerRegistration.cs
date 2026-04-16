namespace NOF.Application;

public sealed record RpcServerRegistration(
    Type ServiceType,
    Type ImplementationType);

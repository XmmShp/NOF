namespace NOF.Hosting.AspNetCore;

public sealed record RpcHttpEndpointHandlerRegistration(
    Type ServiceType,
    string MethodName,
    Delegate Handler,
    Type ReturnType);


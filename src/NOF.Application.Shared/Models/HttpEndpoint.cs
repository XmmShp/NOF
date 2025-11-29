namespace NOF;

public record HttpEndpoint(
    Type RequestType,
    HttpVerb Method,
    string Route,
    string? Permission,
    bool AllowAnonymous
);

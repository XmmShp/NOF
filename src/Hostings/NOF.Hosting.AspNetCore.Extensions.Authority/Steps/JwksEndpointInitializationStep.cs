using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Maps the standard JWKS endpoint (/.well-known/jwks.json) directly as an HTTP GET endpoint.
/// This bypasses the Handler/Result pattern to return raw JWKS JSON as expected by OIDC clients.
/// </summary>
public class JwksEndpointInitializationStep : IEndpointInitializationStep
{
    public Task ExecuteAsync(INOFAppBuilder builder, IHost app)
    {
        if (app is IEndpointRouteBuilder routeBuilder)
        {
            routeBuilder.MapGet("/.well-known/jwks.json", (IJwksService jwksService) =>
            {
                var jwks = jwksService.GetJwks();
                return Results.Ok(jwks);
            })
            .WithName("GetJwks")
            .ExcludeFromDescription();
        }

        return Task.CompletedTask;
    }
}

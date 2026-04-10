using Xunit;

namespace NOF.Contract.Extension.Authorization.Jwt.Tests;

public sealed class JwtAuthorizationEndpointsTests
{
    [Fact]
    public void ShouldMatchWellKnownRoutes()
    {
        Assert.Equal("/.well-known/jwks.json", JwtAuthorizationEndpoints.Jwks);
        Assert.Equal("/connect/token", JwtAuthorizationEndpoints.Token);
        Assert.Equal("/connect/introspect", JwtAuthorizationEndpoints.Introspect);
        Assert.Equal("/connect/revocation", JwtAuthorizationEndpoints.Revocation);
    }
}

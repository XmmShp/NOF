using Microsoft.Extensions.Options;
using NOF.Hosting.Extension.Authorization.Jwt;
using NOF.Test;
using System.Security.Claims;
using Xunit;

namespace NOF.Infrastructure.Extension.Authorization.Jwt.Tests.Extensions;

public sealed class JwtAuthorizationExtensionsTests
{
    [Fact]
    public void JwtId_ShouldExposeStandardJtiClaimType()
    {
        Assert.Equal("jti",
        ClaimTypes.JwtId);
    }

    [Fact]
    public void AddJwksRequestHandler_ShouldReturnSameSelector()
    {
        var builder = NOFTestAppBuilder.Create();
        var selector = new JwtAuthoritySelector(builder);

        var chained = selector.AddJwksRequestHandler();

        Assert.Same(builder, chained.Builder);
    }

    [Fact]
    public async Task AddJwtAuthority_WithIssuerOverload_ShouldRegisterAuthorityServices()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtAuthority("https://issuer.local");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        Assert.NotNull(
        scope.GetRequiredService<JwtAuthorityService>());
        Assert.NotNull(
        scope.GetRequiredService<JwksService>());
        Assert.NotNull(
        scope.GetRequiredService<ISigningKeyService>());
        Assert.Equal("https://issuer.local",
        scope.GetRequiredService<IOptions<JwtAuthorityOptions>>().Value.Issuer);
    }

    [Fact]
    public async Task AddJwtResourceServer_ShouldBridgeTokenPropagationOptions()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtResourceServer(options =>
        {
            options.JwksEndpoint = "https://auth.local/.well-known/jwks.json";
            options.HeaderName = "X-Authorization";
            options.TokenType = "Token";
            options.RequireHttpsMetadata = true;
        });

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        var resourceOptions = scope.GetRequiredService<IOptions<JwtResourceServerOptions>>().Value;
        var propagationOptions = scope.GetRequiredService<IOptions<JwtTokenPropagationOptions>>().Value;
        Assert.Equal("https://auth.local/.well-known/jwks.json",

        resourceOptions.JwksEndpoint);
        Assert.Equal("X-Authorization",
        propagationOptions.HeaderName);
        Assert.Equal("Token",
        propagationOptions.TokenType);
        Assert.NotNull(
        scope.GetRequiredService<IJwksProvider>());
        Assert.IsType<HttpJwksService>(scope.GetRequiredService<HttpJwksService>());
    }
}

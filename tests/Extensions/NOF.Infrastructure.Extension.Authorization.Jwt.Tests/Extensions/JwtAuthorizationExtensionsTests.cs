using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Contract.Extension.Authorization.Jwt;
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
        ClaimTypes.JwtId.Should().Be("jti");
    }

    [Fact]
    public void AddJwksRequestHandler_ShouldReturnSameSelector()
    {
        var builder = NOFTestAppBuilder.Create();
        var selector = new JwtAuthoritySelector(builder);

        var chained = selector.AddJwksRequestHandler();

        chained.Builder.Should().BeSameAs(builder);
    }

    [Fact]
    public async Task AddJwtAuthority_WithIssuerOverload_ShouldRegisterAuthorityServices()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtAuthority("https://issuer.local");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        scope.GetRequiredService<IJwtAuthorityService>().Should().NotBeNull();
        scope.GetRequiredService<IJwksService>().Should().NotBeNull();
        scope.GetRequiredService<IOptions<JwtAuthorityOptions>>().Value.Issuer.Should().Be("https://issuer.local");
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

        resourceOptions.JwksEndpoint.Should().Be("https://auth.local/.well-known/jwks.json");
        propagationOptions.HeaderName.Should().Be("X-Authorization");
        propagationOptions.TokenType.Should().Be("Token");
        scope.GetRequiredService<IJwksProvider>().Should().NotBeNull();
        scope.GetRequiredService<IJwksService>().Should().BeOfType<HttpJwksService>();
    }
}

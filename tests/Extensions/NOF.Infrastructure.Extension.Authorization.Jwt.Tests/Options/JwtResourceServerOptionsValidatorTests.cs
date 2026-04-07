using FluentAssertions;
using Xunit;

namespace NOF.Infrastructure.Extension.Authorization.Jwt.Tests.Options;

public sealed class JwtResourceServerOptionsValidatorTests
{
    [Fact]
    public void Validate_WhenJwksEndpointMissing_ShouldFail()
    {
        var validator = new JwtResourceServerOptionsValidator();

        var result = validator.Validate(null, new JwtResourceServerOptions { JwksEndpoint = "" });

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle(f => f.Contains("JwksEndpoint must be configured."));
    }

    [Fact]
    public void Validate_WhenJwksEndpointNotAbsolute_ShouldFail()
    {
        var validator = new JwtResourceServerOptionsValidator();

        var result = validator.Validate(null, new JwtResourceServerOptions { JwksEndpoint = "/.well-known/jwks.json" });

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle(f => f.Contains("must be an absolute URI"));
    }

    [Fact]
    public void Validate_WhenHttpsRequiredButHttpProvided_ShouldFail()
    {
        var validator = new JwtResourceServerOptionsValidator();

        var result = validator.Validate(null, new JwtResourceServerOptions
        {
            JwksEndpoint = "http://auth.local/.well-known/jwks.json",
            RequireHttpsMetadata = true
        });

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle(f => f.Contains("must use HTTPS"));
    }

    [Fact]
    public void Validate_WhenOptionsAreValid_ShouldSucceed()
    {
        var validator = new JwtResourceServerOptionsValidator();

        var result = validator.Validate(null, new JwtResourceServerOptions
        {
            JwksEndpoint = "https://auth.local/.well-known/jwks.json",
            RequireHttpsMetadata = true
        });

        result.Succeeded.Should().BeTrue();
    }
}

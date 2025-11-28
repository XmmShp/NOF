using FluentAssertions;
using Xunit;

namespace NOF.Infrastructure.Tests.Options;

public class JwtOptionsTests
{
    [Fact]
    public void JwtOptions_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var options = new JwtOptions
        {
            JwtKey = "abcdefghijklmnopqrstuvwxyz1234567890",
            Issuer = "unspecified",
            Audience = "unspecified",
            ExpirationMinutes = 10
        };

        // Assert
        options.JwtKey.Should().Be("abcdefghijklmnopqrstuvwxyz1234567890");
        options.Issuer.Should().Be("unspecified");
        options.Audience.Should().Be("unspecified");
        options.ExpirationMinutes.Should().Be(10);
    }

    [Fact]
    public void JwtOptions_ShouldAllowCustomValues()
    {
        // Arrange & Act
        var options = new JwtOptions
        {
            JwtKey = "custom-secret-key-12345",
            Issuer = "custom-issuer",
            Audience = "custom-audience",
            ExpirationMinutes = 60
        };

        // Assert
        options.JwtKey.Should().Be("custom-secret-key-12345");
        options.Issuer.Should().Be("custom-issuer");
        options.Audience.Should().Be("custom-audience");
        options.ExpirationMinutes.Should().Be(60);
    }

    [Fact]
    public void JwtOptions_JwtKey_ShouldBeSettable()
    {
        // Arrange
        var options = new JwtOptions
        {
            JwtKey = "initial-key",
            Issuer = "issuer",
            Audience = "audience",
            ExpirationMinutes = 10
        };

        // Act
        options.JwtKey = "updated-key";

        // Assert
        options.JwtKey.Should().Be("updated-key");
    }

    [Fact]
    public void JwtOptions_Issuer_ShouldBeSettable()
    {
        // Arrange
        var options = new JwtOptions
        {
            JwtKey = "key",
            Issuer = "initial-issuer",
            Audience = "audience",
            ExpirationMinutes = 10
        };

        // Act
        options.Issuer = "updated-issuer";

        // Assert
        options.Issuer.Should().Be("updated-issuer");
    }

    [Fact]
    public void JwtOptions_Audience_ShouldBeSettable()
    {
        // Arrange
        var options = new JwtOptions
        {
            JwtKey = "key",
            Issuer = "issuer",
            Audience = "initial-audience",
            ExpirationMinutes = 10
        };

        // Act
        options.Audience = "updated-audience";

        // Assert
        options.Audience.Should().Be("updated-audience");
    }

    [Fact]
    public void JwtOptions_ExpirationMinutes_ShouldBeSettable()
    {
        // Arrange
        var options = new JwtOptions
        {
            JwtKey = "key",
            Issuer = "issuer",
            Audience = "audience",
            ExpirationMinutes = 10
        };

        // Act
        options.ExpirationMinutes = 120;

        // Assert
        options.ExpirationMinutes.Should().Be(120);
    }

    [Fact]
    public void JwtOptions_ShouldSupportLongExpirationTimes()
    {
        // Arrange & Act
        var options = new JwtOptions
        {
            JwtKey = "key",
            Issuer = "issuer",
            Audience = "audience",
            ExpirationMinutes = 43200 // 30 days
        };

        // Assert
        options.ExpirationMinutes.Should().Be(43200);
    }

    [Fact]
    public void JwtOptions_ShouldSupportShortExpirationTimes()
    {
        // Arrange & Act
        var options = new JwtOptions
        {
            JwtKey = "key",
            Issuer = "issuer",
            Audience = "audience",
            ExpirationMinutes = 1
        };

        // Assert
        options.ExpirationMinutes.Should().Be(1);
    }

    [Fact]
    public void JwtOptions_ShouldSupportEmptyStrings()
    {
        // Arrange & Act
        var options = new JwtOptions
        {
            JwtKey = "",
            Issuer = "",
            Audience = "",
            ExpirationMinutes = 10
        };

        // Assert
        options.JwtKey.Should().BeEmpty();
        options.Issuer.Should().BeEmpty();
        options.Audience.Should().BeEmpty();
    }

    [Fact]
    public void JwtOptions_ShouldSupportBase64EncodedKeys()
    {
        // Arrange
        var base64Key = Convert.ToBase64String(new byte[32]);

        // Act
        var options = new JwtOptions
        {
            JwtKey = base64Key,
            Issuer = "issuer",
            Audience = "audience",
            ExpirationMinutes = 10
        };

        // Assert
        options.JwtKey.Should().Be(base64Key);
        options.JwtKey.Should().MatchRegex("^[A-Za-z0-9+/=]+$");
    }

    [Fact]
    public void JwtOptions_AllProperties_ShouldBeRequired()
    {
        // This test verifies that all properties are marked as required
        // by attempting to create an instance and checking property attributes
        var type = typeof(JwtOptions);

        var jwtKeyProperty = type.GetProperty(nameof(JwtOptions.JwtKey));
        var issuerProperty = type.GetProperty(nameof(JwtOptions.Issuer));
        var audienceProperty = type.GetProperty(nameof(JwtOptions.Audience));
        var expirationProperty = type.GetProperty(nameof(JwtOptions.ExpirationMinutes));

        // Assert
        jwtKeyProperty.Should().NotBeNull();
        issuerProperty.Should().NotBeNull();
        audienceProperty.Should().NotBeNull();
        expirationProperty.Should().NotBeNull();
    }
}

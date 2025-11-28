using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class ConfigureJwtBearerOptionsTests
{
    [Fact]
    public void Configure_WithJwtBearerScheme_ShouldConfigureOptions()
    {
        // Arrange
        var jwtOptions = new JwtOptions
        {
            JwtKey = Convert.ToBase64String(new byte[32]), // 256-bit key
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 30
        };

        var mockOptions = new Mock<IOptions<JwtOptions>>();
        mockOptions.Setup(o => o.Value).Returns(jwtOptions);

        var configurer = new ConfigureJwtBearerOptions(mockOptions.Object);
        var bearerOptions = new JwtBearerOptions();

        // Act
        configurer.Configure(JwtBearerDefaults.AuthenticationScheme, bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.Should().NotBeNull();
        bearerOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        bearerOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        bearerOptions.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
        bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
        bearerOptions.TokenValidationParameters.ValidIssuer.Should().Be("test-issuer");
        bearerOptions.TokenValidationParameters.ValidAudience.Should().Be("test-audience");
        bearerOptions.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Configure_WithDifferentScheme_ShouldNotConfigureOptions()
    {
        // Arrange
        var jwtOptions = new JwtOptions
        {
            JwtKey = Convert.ToBase64String(new byte[32]),
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 30
        };

        var mockOptions = new Mock<IOptions<JwtOptions>>();
        mockOptions.Setup(o => o.Value).Returns(jwtOptions);

        var configurer = new ConfigureJwtBearerOptions(mockOptions.Object);
        var bearerOptions = new JwtBearerOptions();

        // Act
        configurer.Configure("DifferentScheme", bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.Should().BeEquivalentTo(new TokenValidationParameters());
    }

    [Fact]
    public void Configure_WithoutSchemeName_ShouldConfigureOptions()
    {
        // Arrange
        var jwtOptions = new JwtOptions
        {
            JwtKey = Convert.ToBase64String(new byte[32]),
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 30
        };

        var mockOptions = new Mock<IOptions<JwtOptions>>();
        mockOptions.Setup(o => o.Value).Returns(jwtOptions);

        var configurer = new ConfigureJwtBearerOptions(mockOptions.Object);
        var bearerOptions = new JwtBearerOptions();

        // Act
        configurer.Configure(bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.Should().NotBeNull();
        bearerOptions.TokenValidationParameters.ValidIssuer.Should().Be("test-issuer");
        bearerOptions.TokenValidationParameters.ValidAudience.Should().Be("test-audience");
    }

    [Fact]
    public void Configure_ShouldSetSymmetricSecurityKey()
    {
        // Arrange
        var keyBytes = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            keyBytes[i] = (byte)i;
        }

        var jwtOptions = new JwtOptions
        {
            JwtKey = Convert.ToBase64String(keyBytes),
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 30
        };

        var mockOptions = new Mock<IOptions<JwtOptions>>();
        mockOptions.Setup(o => o.Value).Returns(jwtOptions);

        var configurer = new ConfigureJwtBearerOptions(mockOptions.Object);
        var bearerOptions = new JwtBearerOptions();

        // Act
        configurer.Configure(bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.IssuerSigningKey.Should().BeOfType<SymmetricSecurityKey>();
        var key = (SymmetricSecurityKey)bearerOptions.TokenValidationParameters.IssuerSigningKey;
        key.Key.Should().BeEquivalentTo(keyBytes);
    }

    [Fact]
    public void Configure_ShouldSetClockSkewToZero()
    {
        // Arrange
        var jwtOptions = new JwtOptions
        {
            JwtKey = Convert.ToBase64String(new byte[32]),
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 30
        };

        var mockOptions = new Mock<IOptions<JwtOptions>>();
        mockOptions.Setup(o => o.Value).Returns(jwtOptions);

        var configurer = new ConfigureJwtBearerOptions(mockOptions.Object);
        var bearerOptions = new JwtBearerOptions();

        // Act
        configurer.Configure(bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Configure_ShouldEnableAllValidations()
    {
        // Arrange
        var jwtOptions = new JwtOptions
        {
            JwtKey = Convert.ToBase64String(new byte[32]),
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 30
        };

        var mockOptions = new Mock<IOptions<JwtOptions>>();
        mockOptions.Setup(o => o.Value).Returns(jwtOptions);

        var configurer = new ConfigureJwtBearerOptions(mockOptions.Object);
        var bearerOptions = new JwtBearerOptions();

        // Act
        configurer.Configure(bearerOptions);

        // Assert
        var validationParams = bearerOptions.TokenValidationParameters;
        validationParams.ValidateIssuer.Should().BeTrue();
        validationParams.ValidateAudience.Should().BeTrue();
        validationParams.ValidateLifetime.Should().BeTrue();
        validationParams.ValidateIssuerSigningKey.Should().BeTrue();
    }

    [Fact]
    public void Configure_WithNullSchemeName_ShouldNotConfigureOptions()
    {
        // Arrange
        var jwtOptions = new JwtOptions
        {
            JwtKey = Convert.ToBase64String(new byte[32]),
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 30
        };

        var mockOptions = new Mock<IOptions<JwtOptions>>();
        mockOptions.Setup(o => o.Value).Returns(jwtOptions);

        var configurer = new ConfigureJwtBearerOptions(mockOptions.Object);
        var bearerOptions = new JwtBearerOptions();

        // Act
        configurer.Configure(null, bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.Should().BeEquivalentTo(new TokenValidationParameters());
    }
}

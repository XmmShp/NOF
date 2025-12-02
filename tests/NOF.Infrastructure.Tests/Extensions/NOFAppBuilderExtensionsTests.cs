using FluentAssertions;
using Xunit;

namespace NOF.Infrastructure.Tests.Extensions;

public class NOFAppBuilderExtensionsTests
{
    [Fact]
    public void UseDefaultSettings_RegistrationConfigurators_ShouldNotHaveCircularDependency()
    {
        // Arrange
        var app = NOFApp.CreateApp([]);
        app.UseDefaultSettings();

        // Act - Try to build the dependency graph for registration configurators
        var nofApp = app as NOFApp;
        nofApp.Should().NotBeNull();

        var act = () => new ConfiguratorGraph<IRegistrationConfigurator>(nofApp.RegistrationStages).GetExecutionOrder();

        // Assert - Should not throw circular dependency exception
        act.Should().NotThrow<InvalidOperationException>();
        var executionOrder = act();
        executionOrder.Should().NotBeEmpty();
    }

    [Fact]
    public void UseDefaultSettings_StartupConfigurators_ShouldNotHaveCircularDependency()
    {
        // Arrange
        var app = NOFApp.CreateApp([]);
        app.UseDefaultSettings();

        // Act - Try to build the dependency graph for startup configurators
        var nofApp = app as NOFApp;
        nofApp.Should().NotBeNull();

        var act = () => new ConfiguratorGraph<IStartupConfigurator>(nofApp.StartupStages).GetExecutionOrder();

        // Assert - Should not throw circular dependency exception
        act.Should().NotThrow<InvalidOperationException>();
        var executionOrder = act();
        executionOrder.Should().NotBeEmpty();
    }


    [Fact]
    public void UseDefaultSettings_ShouldAddExpectedConfigurators()
    {
        // Arrange
        var app = NOFApp.CreateApp([]);

        // Act
        app.UseDefaultSettings();

        // Assert
        var nofApp = app as NOFApp;
        nofApp.Should().NotBeNull();

        // Check registration configurators
        nofApp.RegistrationStages.Should().Contain(c => c is ConfigureJsonOptionsConfigurator);
        nofApp.RegistrationStages.Should().Contain(c => c is AddMassTransitConfigurator);
        nofApp.RegistrationStages.Should().Contain(c => c is AddDefaultServicesConfigurator);
        nofApp.RegistrationStages.Should().Contain(c => c is AddSignalRConfigurator);
        nofApp.RegistrationStages.Should().Contain(c => c is AddRedisDistributedCacheConfigurator);
        nofApp.RegistrationStages.Should().Contain(c => c is AddCorsConfigurator);
        nofApp.RegistrationStages.Should().Contain(c => c is AddApiResponseMiddlewareConfigurator);
        nofApp.RegistrationStages.Should().Contain(c => c is AddJwtAuthenticationConfigurator);
        nofApp.RegistrationStages.Should().Contain(c => c is AddAspireConfigurator);

        // Check startup configurators (from combined configurators)
        nofApp.StartupStages.Should().Contain(c => c is AddCorsConfigurator);
        nofApp.StartupStages.Should().Contain(c => c is AddApiResponseMiddlewareConfigurator);
        nofApp.StartupStages.Should().Contain(c => c is AddJwtAuthenticationConfigurator);
        nofApp.StartupStages.Should().Contain(c => c is AddAspireConfigurator);
    }
}

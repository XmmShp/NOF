using FluentAssertions;
using Xunit;

namespace NOF.Infrastructure.Tests.Extensions;

public class NOFAppBuilderExtensionsTests
{
    [Fact]
    public void UseDefaultSettings_StartupConfigurators_ShouldNotHaveCircularDependency()
    {
        // Arrange
        var app = NOFApp.Create([]);
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
        var app = NOFApp.Create([]);

        // Act
        app.UseDefaultSettings();

        // Assert
        var nofApp = app as NOFApp;
        nofApp.Should().NotBeNull();

        nofApp.StartupStages.Should().Contain(c => c is CorsConfigurator);
        nofApp.StartupStages.Should().Contain(c => c is ResponseWrapperConfigurator);
        nofApp.StartupStages.Should().Contain(c => c is JwtAuthenticationConfigurator);
    }
}

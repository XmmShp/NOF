using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace NOF.Infrastructure.Tests.Extensions;

public sealed class HostEnvironmentDeploymentExtensionsTests
{
    [Fact]
    public void InstanceId_ShouldDefaultToOne()
    {
        var environment = new TestHostEnvironment();

        Assert.Equal(1u, environment.InstanceId);
    }

    [Fact]
    public void IsPrimaryNodeEnvironment_ShouldTrackInstanceId_WhenNotCustomized()
    {
        var environment = new TestHostEnvironment();

        Assert.True(environment.IsPrimaryNodeEnvironment);

        environment.InstanceId = 2;

        Assert.False(environment.IsPrimaryNodeEnvironment);
    }

    [Fact]
    public void IsPrimaryNodeEnvironment_ShouldAllowManualOverride()
    {
        var environment = new TestHostEnvironment();
        environment.InstanceId = 2;

        environment.SetPrimaryNodeEnvironmentPredicator(static _ => true);
        environment.InstanceId = 3;

        Assert.True(environment.IsPrimaryNodeEnvironment);
    }

    [Fact]
    public void IsPrimaryNodeEnvironment_ShouldDefaultToInstanceIdEqualsOne()
    {
        var environment = new TestHostEnvironment();

        Assert.True(environment.IsPrimaryNodeEnvironment);

        environment.InstanceId = 2;

        Assert.False(environment.IsPrimaryNodeEnvironment);
    }

    [Fact]
    public void BindConfiguration_ShouldApplyConfiguredInstanceId()
    {
        var environment = new TestHostEnvironment();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [NOFInfrastructureConstants.Deployment.ConfigurationKeys.InstanceId] = "2"
            })
            .Build();

        environment.BindConfiguration(configuration);

        Assert.Equal(2u, environment.InstanceId);
        Assert.False(environment.IsPrimaryNodeEnvironment);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NOF.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class NullFileProvider : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IFileInfo GetFileInfo(string subpath) => new NotFoundFileInfo(subpath);

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }
}

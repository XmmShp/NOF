using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace NOF.Hosting.Tests;

public sealed class HostEnvironmentExtensionsTests
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
    public void BindConfiguration_ShouldApplyConfiguredValues()
    {
        var environment = new TestHostEnvironment();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [NOFHostingConstants.Deployment.ConfigurationKeys.ServiceName] = "Orders.Api",
                [NOFHostingConstants.Deployment.ConfigurationKeys.ServiceId] = "3",
                [NOFHostingConstants.Deployment.ConfigurationKeys.InstanceId] = "2"
            })
            .Build();

        environment.BindConfiguration(configuration);

        Assert.Equal("Orders.Api", environment.ServiceName);
        Assert.Equal(3u, environment.ServiceId);
        Assert.Equal(2u, environment.InstanceId);
        Assert.False(environment.IsPrimaryNodeEnvironment);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NOF.Hosting.Tests";

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

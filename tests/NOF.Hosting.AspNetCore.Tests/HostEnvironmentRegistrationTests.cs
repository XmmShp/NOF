using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Hosting.AspNetCore;
using Xunit;

namespace NOF.Hosting.AspNetCore.Tests;

public sealed class HostEnvironmentRegistrationTests
{
    [Fact]
    public async Task BuildAsync_ShouldResolveBuilderEnvironmentFromServices()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();

        await using var app = await builder.BuildAsync();

        var resolvedEnvironment = app.Services.GetRequiredService<IHostEnvironment>();

        Assert.Same(builder.Environment, resolvedEnvironment);
    }

    [Fact]
    public async Task BuildAsync_InDevelopmentWithoutExplicitPersistence_ShouldSucceed()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.Environment.EnvironmentName = Environments.Development;
        builder.WebApplicationBuilder.WebHost.UseTestServer();

        await using var app = await builder.BuildAsync();

        Assert.NotNull(app.Services.GetRequiredService<NOF.Application.IDbContext>());
    }
}

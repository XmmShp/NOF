using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using Xunit;

namespace NOF.Hosting.AspNetCore.Tests;

public sealed class DaemonServiceResolutionMiddlewareTests
{
    [Fact]
    public async Task Middleware_ShouldResolveScopedDaemonServicesPerRequest()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<DaemonProbe>();
        builder.Services.AddScoped<IDaemonService, RecordingDaemonService>();

        var app = builder.Build();
        app.UseMiddleware<DaemonServiceResolutionMiddleware>();
        app.MapGet("/ping", () => Results.Ok());
        await app.StartAsync();

        try
        {
            using var client = app.GetTestClient();
            using var response = await client.GetAsync("/ping");

            response.EnsureSuccessStatusCode();
            Assert.Equal(1, app.Services.GetRequiredService<DaemonProbe>().ActivationCount);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    private sealed class RecordingDaemonService : IDaemonService
    {
        public RecordingDaemonService(DaemonProbe probe)
        {
            probe.ActivationCount++;
        }
    }

    private sealed class DaemonProbe
    {
        public int ActivationCount { get; set; }
    }
}

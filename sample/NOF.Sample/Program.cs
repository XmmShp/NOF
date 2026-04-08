using ConfigurationCenter;
using NOF.Application;
using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure;
using NOF.Infrastructure.EntityFrameworkCore;
using NOF.Infrastructure.EntityFrameworkCore.PostgreSQL;
using NOF.Infrastructure.Extension.Authorization.Jwt;
using NOF.Infrastructure.RabbitMQ;
using NOF.Infrastructure.StackExchangeRedis;
using NOF.Sample;
using NOF.Sample.Application;
using NOF.Sample.Application.Repositories;
using NOF.Sample.Repositories;

var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.AddApplicationPart(typeof(NOFSampleService).Assembly)
    .AddApplicationPart(typeof(JwtAuthorityService).Assembly);

builder.AddRedisCache();

builder.AddJwtAuthority(o => o.Issuer = "NOF.Sample");

builder.AddJwtResourceServer(o =>
{
    o.Issuer = "NOF.Sample";
    o.RequireHttpsMetadata = false;
    o.JwksEndpoint = "http://localhost/.well-known/jwks.json";
});

builder.AddRabbitMQ();

builder.AddEFCore<ConfigurationDbContext>()
    .UseSharedDatabaseTenancy()
    .AutoMigrate()
    .UsePostgreSQL();

builder.Services.AddScoped<IConfigNodeChildrenRepository, ConfigNodeChildrenRepository>();

builder.Services.AddAntDesign();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHostedService(async (sp, ct) =>
{
    while (!ct.IsCancellationRequested)
    {
        await using var scope = sp.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var taskId = Random.Shared.Next().ToString();
        await publisher.PublishAsync(new TaskStarted(taskId), cancellationToken: ct);
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await publisher.PublishAsync(new TaskContinued(taskId), cancellationToken: ct);
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
    }
});

var app = await builder.BuildAsync();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(NOF.Sample.UI.Components.Routes).Assembly,
        typeof(NOF.Sample.Wasm.WasmMarker).Assembly);

app.MapServiceToHttpEndpoints<INOFSampleService>();
app.MapServiceToHttpEndpoints<IJwtAuthorityService>();
app.MapServiceToHttpEndpoints<IJwksService>();

await app.RunAsync();

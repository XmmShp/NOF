using ConfigurationCenter;
using NOF.Application;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure;
using NOF.Infrastructure.EntityFrameworkCore;
using NOF.Infrastructure.EntityFrameworkCore.PostgreSQL;
using NOF.Infrastructure.Extension.Authorization.Jwt;
using NOF.Infrastructure.MassTransit;
using NOF.Infrastructure.MassTransit.RabbitMQ;
using NOF.Infrastructure.StackExchangeRedis;
using NOF.Sample;
using NOF.Sample.Application;

var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.Services.AddNOFSampleAutoInjectServices();
builder.Services.AddAllHandlers()
    .AddStateMachineHandlers(NOFSampleApplicationStateMachineHandlers.Handlers);

builder.Services.Configure<MapperOptions>(o => o.ConfigureAutoMappings());

builder.AddRedisCache();

builder.AddJwtAuthority(o => o.Issuer = "NOF.Sample")
    .AddJwksRequestHandler();

builder.AddJwtAuthorization(o => o.Issuer = "NOF.Sample");

builder.AddMassTransit()
    .UseRabbitMQ();

builder.AddEFCore<ConfigurationDbContext>()
    .AutoMigrate()
    .UsePostgreSQL();

builder.Services.AddAntDesign();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddScoped<INOFSampleService, RequestSenderNOFSampleService>();

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
        typeof(NOF.Sample.UI._Imports).Assembly,
        typeof(NOF.Sample.Wasm._Imports).Assembly);

app.MapAllHttpEndpoints();

await app.RunAsync();

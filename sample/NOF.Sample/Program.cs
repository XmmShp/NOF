using NOF.Application;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure.Core;
using NOF.Infrastructure.EntityFrameworkCore;
using NOF.Infrastructure.EntityFrameworkCore.PostgreSQL;
using NOF.Infrastructure.Extension.Authorization.Jwt;
using NOF.Infrastructure.MassTransit;
using NOF.Infrastructure.MassTransit.RabbitMQ;
using NOF.Infrastructure.StackExchangeRedis;
using NOF.Sample;
using NOF.Sample.Contract;
using NOF.Sample.WebUI;

var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.Services.AddNOFSampleAutoInjectServices();
builder.Services.AddAllHandlers();

builder.AddRedisCache();

builder.AddJwtAuthority()
    .AddJwksRequestHandler();

builder.AddJwtAuthorization();

builder.AddMassTransit()
    .UseRabbitMQ();

builder.AddEFCore<ConfigurationDbContext>()
    .AutoMigrate()
    .UsePostgreSQL();

builder.Services.AddAntDesign()
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Here, we self-call for test
builder.Services.AddHttpClient<INOFSampleService, NOFSampleServiceClient>(client => client.BaseAddress = new Uri("http://localhost:55892/"));

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
    .AddInteractiveServerRenderMode();

app.MapAllHttpEndpoints();

await app.RunAsync();

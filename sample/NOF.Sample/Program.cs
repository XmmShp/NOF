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

builder.AddApplicationPart(typeof(NOFSampleService).Assembly);

builder.Services.Configure<MapperOptions>(o => o.ConfigureAutoMappings());

builder.AddRedisCache();

builder.AddJwtAuthority(o => o.Issuer = "NOF.Sample");

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

builder.Services.AddScoped<INOFSampleService>(sp =>
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    var request = accessor.HttpContext?.Request;
    var baseUri = request is null
        ? "http://localhost:55892/"
        : $"{request.Scheme}://{request.Host}/";

    return new HttpNOFSampleService(new HttpClient
    {
        BaseAddress = new Uri(baseUri)
    });
});

builder.Services.AddScoped<IJwtAuthorityService>(sp =>
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    var request = accessor.HttpContext?.Request;
    var baseUri = request is null
        ? "http://localhost:55892/"
        : $"{request.Scheme}://{request.Host}/";

    return new HttpSampleJwtAuthorityService(new HttpClient
    {
        BaseAddress = new Uri(baseUri)
    });
});

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

app.MapAllHttpEndpoints();

await app.RunAsync();

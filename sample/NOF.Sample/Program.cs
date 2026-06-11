using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Hosting;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure;
using NOF.Infrastructure.Extension.Authentication;
using NOF.Infrastructure.RabbitMQ;
using NOF.Infrastructure.StackExchangeRedis;
using NOF.Sample;
using NOF.Sample.Application;
using NOF.Sample.Services;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddApplicationPart(typeof(NOFSampleService).Assembly);

builder.AddRedisCache(builder.Configuration.GetConnectionString("redis"));

builder.AddAuthenticationAuthority(o =>
{
    o.Issuer = "http://localhost/oauth2";
    o.SigningKeyEncryptionKey = builder.Configuration["NOF:Authority:SigningKeyEncryptionKey"]
        ?? throw new InvalidOperationException("Configuration value 'NOF:Authority:SigningKeyEncryptionKey' not found.");
});

builder.AddOAuthAuthorizationServer(o =>
{
    o.Issuer = "http://localhost/oauth2";
    o.AccessTokenAudience = "nof-sample";
});

builder.AddAuthenticationResourceServer(o =>
{
    o.Issuer = "http://localhost/oauth2";
    o.RequireHttpsMetadata = false;
    o.JwksEndpoint = "http://localhost/oauth2/.well-known/jwks.json";
});

builder.AddRabbitMQ(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
});

builder.UseDbContext<ConfigurationDbContext>()
    .WithTenantMode(TenantMode.DatabasePerTenant)
    .WithConnectionString(builder.Configuration.GetConnectionString("postgres")
        ?? throw new InvalidOperationException("Connection string 'postgres' not found in configuration."))
    .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseNpgsql(connectionString))
    .MigrateOnInitialize();

builder.Services.ReplaceOrAddScoped<INOFSampleServiceClient, LocalNOFSampleServiceClient>();
builder.Services.AddScoped<IOAuthAuthorizationHandler, SampleOAuthAuthorizationHandler>();
builder.Services.AddScoped<IOAuthSubjectService, SampleOAuthSubjectService>();

builder.Services.AddAntDesign();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHostedService(async (sp, ct) =>
{
    while (!ct.IsCancellationRequested)
    {
        await using var scope = sp.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var context = scope.ServiceProvider.GetRequiredService<IContextAccessor>().Context;
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var taskId = Random.Shared.Next().ToString();
        await publisher.PublishAsync(new TaskStarted(taskId), context, ct);
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await publisher.PublishAsync(new TaskContinued(taskId), context, ct);
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
    }
});

var app = await builder.BuildAsync();

app.MapOpenApi();
app.UseAntiforgery();

app.MapHttpEndpoint<NOFSampleService>();
app.MapHttpEndpoint<OAuthAuthorizationServerService>();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(NOF.Sample.UI.Components.Routes).Assembly,
        typeof(NOF.Sample.Wasm.WasmMarker).Assembly);

await app.RunAsync();

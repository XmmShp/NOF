using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using NOF.Hosting.AspNetCore;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using NOF.Infrastructure.EntityFrameworkCore;
using NOF.Sample;
using NOF.Sample.Application;
using NOF.Sample.Services;

var builder = NOFWebApplicationBuilder.Create(args);
var sampleOrigin = ResolveSampleOrigin(builder.Configuration);
var sampleIssuer = $"{sampleOrigin}/oauth2";

builder.AddApplicationPart(typeof(NOFSampleService).Assembly);
builder.AddRpcServer<NOFSampleService>();
builder.AddRpcServer<OAuthChainDemoService>();
builder.AddRpcServer<DemoDownstreamService>();

builder.Services.AddRedisCache(builder.Configuration.GetConnectionString("redis"));

builder.AddOidcServer(o =>
{
    o.Issuer = sampleIssuer;
    o.AccessTokenAudience = "nof-sample";
    o.SigningKeyEncryptionKey = builder.Configuration["NOF:OidcServer:SigningKeyEncryptionKey"]
        ?? throw new InvalidOperationException("Configuration value 'NOF:OidcServer:SigningKeyEncryptionKey' not found.");
})
.AddPublicClient(
    "nof-sample-ui",
    ["openid", "profile", "email", "sample.read", "sample.write"],
    displayName: "NOF Sample UI");

builder.Services.AddAuthenticationResourceServer(o =>
{
    o.ExpectedIssuer = sampleIssuer;
    o.RequireHttpsMetadata = false;
    o.AuthorizationServerIssuer = sampleIssuer;
});

builder.Services.AddRabbitMQ(options =>
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
builder.Services.ReplaceOrAddScoped<IOAuthChainDemoServiceClient, LocalOAuthChainDemoServiceClient>();
builder.Services.AddScoped<IOAuthAuthorizationHandler, SampleOAuthAuthorizationHandler>();
builder.Services.AddScoped<IOAuthSubjectService, SampleOAuthSubjectService>();
builder.Services.AddHttpClient<OAuthChainDemoBackend>(client => client.BaseAddress = new Uri(sampleOrigin));
builder.Services.AddRequestOutboundMiddleware<OAuthChainDemoAccessTokenOutboundMiddleware>();
builder.Services.AddHttpClient<SelfHttpDemoDownstreamServiceClient>(client => client.BaseAddress = new Uri(sampleOrigin));
builder.Services.AddScoped<IDemoDownstreamServiceClient>(static serviceProvider =>
    serviceProvider.GetRequiredService<SelfHttpDemoDownstreamServiceClient>());

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
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var taskId = Random.Shared.Next().ToString();
        await publisher.PublishAsync(new TaskStarted(taskId), Context.Empty, ct);
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await publisher.PublishAsync(new TaskContinued(taskId), Context.Empty, ct);
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
    }
});

var app = await builder.BuildAsync();

app.MapOpenApi();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(NOF.Sample.UI.Components.Routes).Assembly,
        typeof(NOF.Sample.Wasm.WasmMarker).Assembly);

await app.RunAsync();

static string ResolveSampleOrigin(IConfiguration configuration)
{
    var urls = configuration["ASPNETCORE_URLS"];
    if (!string.IsNullOrWhiteSpace(urls))
    {
        var candidate = urls
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Authority}";
        }
    }

    return "http://localhost";
}

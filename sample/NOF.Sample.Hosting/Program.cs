using MassTransit;
using Microsoft.Extensions.Options;
using NOF;
using NOF.Sample;
using NOF.Sample.Application.RequestHandlers;
using NOF.Sample.WebUI;
using Yitter.IdGenerator;

var builder = NOFWebApplicationBuilder.Create(args, autoInject: true, useDefaultConfigs: true, autoMapEndpoints: true);

builder.WithApplicationPart<ConfigNode>()
    .WithApplicationPart<GetRootConfigNodesRequest>()
    .WithApplicationPart<GetRootConfigNodes>()
    .WithApplicationPart<GetConfigurationRequest>();

builder.AddObservability()
    .AddEFCore<ConfigurationDbContext>()
    .AutoMigrate(builder)
    .UsePostgreSQL();

builder.AddMassTransit(builder)
    .UseEFCoreOutbox(o => o.UsePostgres())
    .UseRabbitMQ();

builder.Services.AddOptionsInConfiguration<IdGeneratorOptions>();
builder.Services.AddAntDesign()
.AddRazorComponents()
.AddInteractiveServerComponents();

// Here, we self-call for test
builder.Services.AddHttpClient<INOFService, NOFServiceClient>(client => client.BaseAddress = new Uri("http://localhost:55892/"));

var app = await builder.BuildAsync();
YitIdHelper.SetIdGenerator(app.Services.GetRequiredService<IOptions<IdGeneratorOptions>>().Value);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();

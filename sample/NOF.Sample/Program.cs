using Microsoft.Extensions.Options;
using NOF;
using NOF.Generated;
using NOF.Sample;
using NOF.Sample.Application.CommandHandlers;
using NOF.Sample.WebUI;
using Yitter.IdGenerator;

var builder = NOFApp.Create(args);

builder.UseDefaultSettings()
.AddObservability()
.AddEFCore<ConfigurationDbContext>()
    .AutoMigrate()
    .UsePostgreSQL()
.AddMassTransit(typeof(GetConfiguration))
    .UseEFCoreOutbox()
    .UseRabbitMQ();

builder.Services.AddOptionsInConfiguration<IdGeneratorOptions>();
builder.Services.AddAutoInjectServices()
    .AddAntDesign()
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Here, we self-call for test
builder.Services.AddHttpClient<INOFService, NOFServiceClient>(client => client.BaseAddress = new Uri("http://localhost:55892/"));

var app = await builder.BuildAsync();
YitIdHelper.SetIdGenerator(app.Services.GetRequiredService<IOptions<IdGeneratorOptions>>().Value);

app.MapAllHttpEndpoints();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();

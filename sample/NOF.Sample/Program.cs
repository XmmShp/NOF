using MassTransit;
using Microsoft.Extensions.Options;
using NOF;
using NOF.Generated;
using NOF.Sample;
using NOF.Sample.WebUI;
using Yitter.IdGenerator;

var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.WithAutoApplicationParts();

builder.Services.AddNOF_SampleAutoInjectServices();

builder.Services.AddRedisCache();

builder.AddMassTransit()
    .UseEFCoreOutbox(o => o.UsePostgres())
    .UseRabbitMQ();

builder.AddEFCore<ConfigurationDbContext>()
    .AutoMigrate()
    .UsePostgreSQL();

builder.Services.AddOptionsInConfiguration<IdGeneratorOptions>();
builder.Services.AddAntDesign()
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redis");
});

// Here, we self-call for test
builder.Services.AddHttpClient<INOFService, NOFServiceClient>(client => client.BaseAddress = new Uri("http://localhost:55892/"));

builder.Services.AddHostedService(async (sp, ct) =>
{
    while (!ct.IsCancellationRequested)
    {
        await using var scope = sp.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await publisher.PublishAsync(new TaskStarted(Random.Shared.Next().ToString()), ct);
        await uow.SaveChangesAsync(ct);
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
    }
});

var app = await builder.BuildAsync();
YitIdHelper.SetIdGenerator(app.Services.GetRequiredService<IOptions<IdGeneratorOptions>>().Value);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAllHttpEndpoints();

await app.RunAsync();

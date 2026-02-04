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

builder.AddJwtAuthentication();

builder.AddMassTransit()
    .UseRabbitMQ();

builder.AddEFCore<ConfigurationDbContext, ConfigurationPublicDbContext>()
    .AutoMigrate()
    .UsePostgreSQL();

builder.Services.AddOptionsInConfiguration<IdGeneratorOptions>();
builder.Services.AddScoped<NOF.Sample.WebUI.Services.JwtAuthService>();
builder.Services.AddAntDesign()
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Here, we self-call for test
builder.Services.AddHttpClient<INOFService, NOFServiceClient>(client => client.BaseAddress = new Uri("http://localhost:55892/"));

builder.Services.AddHostedService(async (sp, ct) =>
{
    while (!ct.IsCancellationRequested)
    {
        await using var scope = sp.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var taskId = Random.Shared.Next().ToString();
        await publisher.PublishAsync(new TaskStarted(taskId), ct);
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await publisher.PublishAsync(new TaskContinued(taskId), ct);
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
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

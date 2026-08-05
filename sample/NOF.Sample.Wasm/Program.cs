using NOF.Hosting.BlazorWebAssembly;
using NOF.Sample;

var builder = NOFWebAssemblyHostBuilder.Create(args);

builder.Services.AddAntDesign();
builder.Services.AddScoped<INOFSampleServiceClient, HttpNOFSampleServiceClient>();
builder.Services.AddScoped<IOAuthChainDemoServiceClient, HttpOAuthChainDemoServiceClient>();
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.WebAssemblyHostBuilder.HostEnvironment.BaseAddress)
});

var app = await builder.BuildAsync();
await app.RunAsync();

using NOF.Hosting.BlazorWebAssembly;
using NOF.Infrastructure.Extension.Authorization.Jwt;
using NOF.Sample;
using NOF.Sample.UI.Services;

var builder = NOFWebAssemblyHostBuilder.Create(args);

builder.Services.AddAntDesign();
builder.Services.AddScoped<HttpNOFSampleService>();
builder.Services.AddScoped<INOFSampleServiceClient>(sp => sp.GetRequiredService<HttpNOFSampleService>());
builder.Services.AddScoped<HttpJwtAuthorityService>();
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.WebAssemblyHostBuilder.HostEnvironment.BaseAddress)
});

var app = await builder.BuildAsync();
await app.RunAsync();

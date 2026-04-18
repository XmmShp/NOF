using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting.BlazorWebAssembly;
using NOF.Sample;
using NOF.Sample.UI.Services;

var builder = NOFWebAssemblyHostBuilder.Create(args);

builder.Services.AddAntDesign();
builder.Services.AddScoped<INOFSampleServiceClient, HttpNOFSampleService>();
builder.Services.AddScoped<IJwtAuthorityServiceClient, HttpJwtAuthorityService>();
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.WebAssemblyHostBuilder.HostEnvironment.BaseAddress)
});

var app = await builder.BuildAsync();
await app.RunAsync();

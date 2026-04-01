using AntDesign;
using NOF.Hosting.BlazorWebAssembly;
using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Sample;

var builder = NOFWebAssemblyHostBuilder.Create(args);

builder.Services.AddAntDesign();
builder.Services.AddScoped<INOFSampleService, HttpNOFSampleService>();
builder.Services.AddScoped<IJwtAuthorityService, HttpSampleJwtAuthorityService>();
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.WebAssemblyHostBuilder.HostEnvironment.BaseAddress)
});

var app = await builder.BuildAsync();
await app.RunAsync();

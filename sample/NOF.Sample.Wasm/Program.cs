using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting.BlazorWebAssembly;
using NOF.Infrastructure.Extension.Authorization.Jwt;
using NOF.Sample;
using NOF.Sample.Wasm.Services;

var builder = NOFWebAssemblyHostBuilder.Create(args);

builder.Services.AddAntDesign();
builder.Services.AddScoped<INOFSampleService, HttpNOFSampleService>();
builder.Services.AddScoped<IJwtAuthorityService, HttpJwtAuthorityService>();
builder.Services.AddScoped<IJwksService, HttpJwksService>();
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.WebAssemblyHostBuilder.HostEnvironment.BaseAddress)
});

var app = await builder.BuildAsync();
await app.RunAsync();

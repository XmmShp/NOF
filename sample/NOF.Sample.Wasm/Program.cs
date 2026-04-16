using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting.BlazorWebAssembly;
using NOF.Infrastructure.Extension.Authorization.Jwt;
using NOF.Sample;

var builder = NOFWebAssemblyHostBuilder.Create(args);

builder.Services.AddAntDesign();
builder.Services.AddScoped<NOF.Sample.UI.Services.HttpNOFSampleService>();
builder.Services.AddScoped<HttpJwtAuthorityService>();
builder.Services.AddScoped<HttpJwksService>();
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.WebAssemblyHostBuilder.HostEnvironment.BaseAddress)
});

var app = await builder.BuildAsync();
await app.RunAsync();

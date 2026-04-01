using Microsoft.Extensions.Logging;
using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting.Maui;

namespace NOF.Sample.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = NOFMauiAppBuilder.Create();

        builder.MauiAppBuilder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddAntDesign();
        builder.Services.AddScoped<INOFSampleService, HttpNOFSampleService>();
        builder.Services.AddScoped<IJwtAuthorityService, HttpSampleJwtAuthorityService>();
        builder.Services.AddScoped(_ => new HttpClient
        {
            BaseAddress = new Uri(builder.Configuration["SampleApiBaseAddress"] ?? "https://localhost:5001/")
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.BuildAsync().GetAwaiter().GetResult();
        return app.MauiApp;
    }
}

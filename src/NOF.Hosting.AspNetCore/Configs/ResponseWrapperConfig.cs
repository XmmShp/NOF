using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace NOF;

public class ResponseWrapperConfig<THostApplication> : IResponseFormattingConfig<THostApplication>
    where THostApplication : class, IHost, IApplicationBuilder
{
    public Task ExecuteAsync(INOFAppBuilder builder, THostApplication app)
    {
        app.UseMiddleware<ResponseWrapperMiddleware>();
        return Task.CompletedTask;
    }
}
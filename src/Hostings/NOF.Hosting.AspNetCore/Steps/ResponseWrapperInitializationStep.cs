using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace NOF;

public class ResponseWrapperInitializationStep : IResponseFormattingInitializationStep
{
    public Task ExecuteAsync(INOFAppBuilder builder, IHost app)
    {
        (app as IApplicationBuilder)?.UseMiddleware<ResponseWrapperMiddleware>();
        return Task.CompletedTask;
    }
}
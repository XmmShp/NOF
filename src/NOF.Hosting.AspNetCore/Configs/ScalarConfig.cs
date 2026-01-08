using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

namespace NOF;

public class ScalarConfig : IEndpointConfig
{
    public Task ExecuteAsync(INOFAppBuilder builder, IHost app)
    {
        (app as IEndpointRouteBuilder)?.MapOpenApi();
        (app as IEndpointRouteBuilder)?.MapScalarApiReference();
        return Task.CompletedTask;
    }
}
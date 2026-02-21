using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using NOF.Infrastructure.Abstraction;
using Scalar.AspNetCore;

namespace NOF.Hosting.AspNetCore;

public class ScalarInitializationStep : IEndpointInitializationStep
{
    public Task ExecuteAsync(IApplicationInitializationContext context, IHost app)
    {
        (app as IEndpointRouteBuilder)?.MapOpenApi();
        (app as IEndpointRouteBuilder)?.MapScalarApiReference();
        return Task.CompletedTask;
    }
}

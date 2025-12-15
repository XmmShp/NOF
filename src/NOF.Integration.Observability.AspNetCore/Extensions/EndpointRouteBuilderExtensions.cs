using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

namespace NOF;

public static partial class __NOF_Integration_Observability_AspNetCore__
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    extension(IEndpointRouteBuilder builder)
    {
        internal IEndpointRouteBuilder MapHealthCheckEndpoints()
        {
            builder.MapHealthChecks(HealthEndpointPath);
            builder.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });

            return builder;
        }
    }
}
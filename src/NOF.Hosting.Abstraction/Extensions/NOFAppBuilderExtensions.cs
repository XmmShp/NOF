using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddHostingDefaults()
        {
            builder.Services.TryAddSingleton(builder.Environment);
            builder.Services.AddNOFAbstraction();
            builder.Services.TryAddScoped<RequestOutboundPipelineExecutor>();
            builder.Services.TryAddTransient(typeof(Lazy<>), typeof(NOFLazy<>));
            return builder;
        }
    }
}

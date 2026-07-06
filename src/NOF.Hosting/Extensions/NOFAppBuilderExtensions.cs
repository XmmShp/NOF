using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddNOFHosting()
        {
            builder.Services.TryAddSingleton(builder.Environment);
            builder.Services.AddNOFHosting();
            return builder;
        }
    }
}

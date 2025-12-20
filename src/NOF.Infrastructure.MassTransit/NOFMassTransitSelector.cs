using Microsoft.Extensions.Hosting;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Integration.Tests")]

namespace NOF;

public interface INOFMassTransitSelector<THostApplication>
    where THostApplication : class, IHost
{
    public INOFAppBuilder<THostApplication> Builder { get; }
}

public class NOFMassTransitSelector<THostApplication> : INOFMassTransitSelector<THostApplication>
    where THostApplication : class, IHost
{
    public INOFAppBuilder<THostApplication> Builder { get; }
    public NOFMassTransitSelector(INOFAppBuilder<THostApplication> builder)
    {
        Builder = builder;
    }
}

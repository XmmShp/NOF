using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Integration.Tests")]

namespace NOF;

public interface INOFMassTransitSelector
{
    INOFAppBuilder Builder { get; }
}

public class NOFMassTransitSelector : INOFMassTransitSelector
{
    public INOFAppBuilder Builder { get; }
    public NOFMassTransitSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }
}

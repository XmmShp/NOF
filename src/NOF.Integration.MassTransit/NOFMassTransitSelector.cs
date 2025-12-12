using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Integration.Tests")]

namespace NOF;

public interface INOFMassTransitSelector
{
    public INOFApp App { get; }
}

public class NOFMassTransitSelector : INOFMassTransitSelector
{
    public INOFApp App { get; }
    public NOFMassTransitSelector(INOFApp app)
    {
        App = app;
    }
}

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Test")]

namespace NOF;

public interface IEFCoreSelector
{
    INOFAppBuilder Builder { get; }
}

internal class EFCoreSelector : IEFCoreSelector
{
    public INOFAppBuilder Builder { get; }
    public EFCoreSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }
}

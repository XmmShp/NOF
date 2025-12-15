namespace NOF;

public interface INOFEFCoreSelector
{
    public INOFAppBuilder Builder { get; }
}

internal class NOFEFCoreSelector : INOFEFCoreSelector
{
    public INOFAppBuilder Builder { get; }
    public NOFEFCoreSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }
}
namespace NOF;

public interface INOFEFCoreSelector
{
    public INOFApp App { get; }
}

internal class NOFEFCoreSelector : INOFEFCoreSelector
{
    public INOFApp App { get; }
    public NOFEFCoreSelector(INOFApp app)
    {
        App = app;
    }
}
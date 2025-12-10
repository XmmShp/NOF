namespace NOF;

public interface INOFEFCoreApp<TDbContext>
    where TDbContext : NOFDbContext
{
    public INOFApp App { get; }
}

public class NOFEFCoreApp<TDbContext> : INOFEFCoreApp<TDbContext>
    where TDbContext : NOFDbContext
{
    public INOFApp App { get; }
    public NOFEFCoreApp(INOFApp app)
    {
        App = app;
    }
}
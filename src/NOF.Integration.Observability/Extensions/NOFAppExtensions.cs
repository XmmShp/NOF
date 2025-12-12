namespace NOF;

public static partial class __NOF_Infrastructure_Observability__
{
    extension(INOFApp app)
    {
        public INOFApp AddObservability()
        {
            app.Unwrap().AddObservabilities();
            app.AddStartupConfigurator<ObservabilityConfigurator>();
            return app;
        }
    }
}

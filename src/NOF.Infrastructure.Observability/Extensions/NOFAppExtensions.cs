namespace NOF;

public static partial class __NOF_Infrastructure_Observability__
{
    extension(INOFApp app)
    {
        public INOFApp AddObservability()
        {
            app.AddRegistrationConfigurator<ObservabilityConfigurator>();
            return app;
        }
    }
}

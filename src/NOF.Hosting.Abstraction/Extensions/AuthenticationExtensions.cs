namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddJwtPropagation()
        {
            builder.Services.AddRequestOutboundMiddleware<JwtTokenPropagationOutboundMiddleware>();
            JwtPropagationRegistrationHooks.Invoke(builder);
            return builder;
        }
    }
}

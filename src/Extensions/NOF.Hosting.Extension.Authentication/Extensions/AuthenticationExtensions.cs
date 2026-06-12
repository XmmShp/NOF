namespace NOF.Hosting.Extension.Authentication;

public static partial class NOFAuthenticationExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddAccessTokenPropagation()
        {
            builder.Services.AddRequestOutboundMiddleware<AccessTokenPropagationOutboundMiddleware>();
            return builder;
        }
    }
}

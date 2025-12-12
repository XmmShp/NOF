using Microsoft.AspNetCore.Builder;

namespace NOF;

public static partial class __NOF_Infrastructure_Extensions__
{
    extension<TBuilder>(TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        public TBuilder RequirePermission(string permission)
        {
            builder.WithMetadata(new RequirePermissionAttribute(permission));
            return builder;
        }
    }
}

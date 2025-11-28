using Microsoft.AspNetCore.Builder;

namespace NOF;

public static class EndpointConventionBuilderExtensions
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

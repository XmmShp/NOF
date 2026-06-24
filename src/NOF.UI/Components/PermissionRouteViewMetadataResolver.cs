using NOF.Contract;

namespace NOF.UI.Components;

internal static class PermissionRouteViewMetadataResolver
{
    internal static string? ResolveRequiredPermission(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        for (var current = pageType; current is not null; current = current.BaseType)
        {
            var metadata = current.GetCustomAttributes(inherit: false)
                .OfType<MetadataAttribute>()
                .LastOrDefault(static attribute => string.Equals(
                    attribute.Key,
                    RequirePermissionAttribute.MetadataKey,
                    StringComparison.OrdinalIgnoreCase));
            if (metadata is not null)
            {
                return metadata.Value;
            }
        }

        return null;
    }
}

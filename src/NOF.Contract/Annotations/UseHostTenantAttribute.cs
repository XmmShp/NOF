using System.Reflection;

namespace NOF.Contract;

/// <summary>
/// Executes an RPC operation in the host tenant after normal authorization succeeds.
/// Apply this attribute to the RPC contract method. It does not grant permissions or bypass authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class UseHostTenantAttribute : MetadataAttribute
{
    /// <summary>Gets the metadata key identifying host-tenant RPC operations.</summary>
    public const string MetadataKey = "nof.tenant.use_host";

    /// <summary>Marks an RPC contract method for host-tenant execution.</summary>
    public UseHostTenantAttribute()
        : base(MetadataKey, bool.TrueString)
    {
    }

    /// <summary>
    /// Checks whether a contract method requests host-tenant execution, including equivalent metadata attributes.
    /// Client tenant selectors can use this to recognize host operations.
    /// </summary>
    public static bool IsRequired(MemberInfo? serviceMethodInfo)
        => serviceMethodInfo?.GetCustomAttributes<MetadataAttribute>(inherit: true)
            .Any(static attribute => string.Equals(attribute.Key, MetadataKey, StringComparison.OrdinalIgnoreCase)
                && bool.TryParse(attribute.Value, out var enabled)
                && enabled) == true;
}

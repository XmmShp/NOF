using NOF.Annotation;

namespace NOF.Contract;

/// <summary>
/// Permission requirement attribute for marking handlers or messages that need specific permissions
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class RequirePermissionAttribute : MetadataAttribute
{
    public const string MetadataKey = "api.permission";

    /// <summary>
    /// Required permission value.
    /// Empty string means authentication is required but no specific permission is required.
    /// </summary>
    public string Permission => Value ?? string.Empty;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="permission">
    /// Required permission value. Use empty string to require authentication only.
    /// </param>
    public RequirePermissionAttribute(string permission = "")
        : base(MetadataKey, permission)
    {
    }
}

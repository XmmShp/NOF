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
    /// Required permission, if null then only authentication is required
    /// </summary>
    public string? Permission => Value;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="permission">Required permission, if null then only authentication is required</param>
    public RequirePermissionAttribute(string? permission = null)
        : base(MetadataKey, permission ?? string.Empty)
    {
    }
}

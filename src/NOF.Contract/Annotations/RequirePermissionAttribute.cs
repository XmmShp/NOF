namespace NOF.Contract;

/// <summary>
/// Permission requirement attribute for marking handlers or messages that need specific permissions
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public class RequirePermissionAttribute : Attribute
{
    /// <summary>
    /// Required permission, if null then only authentication is required
    /// </summary>
    public string? Permission { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="permission">Required permission, if null then only authentication is required</param>
    public RequirePermissionAttribute(string? permission = null)
    {
        Permission = permission;
    }
}

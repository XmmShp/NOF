using NOF.Annotation;

namespace NOF.Contract;

/// <summary>
/// Marks an API as allowing anonymous access.
/// Implemented as <c>api.permission = null</c> so it can override class-level permission requirements.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class AllowAnonymousAttribute : MetadataAttribute
{
    public AllowAnonymousAttribute()
        : base(RequirePermissionAttribute.MetadataKey, null)
    {
    }
}


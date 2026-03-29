namespace NOF.Contract;

/// <summary>
/// Marks a handler or message to allow anonymous access without authentication
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class AllowAnonymousAttribute : Attribute;


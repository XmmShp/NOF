namespace NOF.Contract;

/// <summary>
/// Marks a handler or message to allow anonymous access without authentication
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class AllowAnonymousAttribute : Attribute;

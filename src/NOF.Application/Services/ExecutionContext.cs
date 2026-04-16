using NOF.Abstraction;

namespace NOF.Application;

/// <summary>
/// Execution context contract used across the application layer to carry ambient metadata (headers, tenant, tracing).
/// </summary>
public interface IExecutionContext : IDictionary<string, string?> { }

/// <summary>
/// Default implementation of IExecutionContext.
/// </summary>
public sealed class ExecutionContext : Dictionary<string, string?>, IExecutionContext
{
    public ExecutionContext() : base(StringComparer.OrdinalIgnoreCase) { }
}

public static partial class ExecutionContextExtensions
{
    extension(IExecutionContext context)
    {
        public string TenantId
        {
            get
            {
                context.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId);
                return NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId);
            }
            set
            {
                context[NOFAbstractionConstants.Transport.Headers.TenantId] = NOFAbstractionConstants.Tenant.NormalizeTenantId(value);
            }
        }
    }
}

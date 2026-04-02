using System.Diagnostics;

namespace NOF.Contract;

/// <summary>
/// Represents the context of a logical execution (e.g., HTTP request, command execution,
/// event handling, background task). Provides ambient access to headers, tenant, and tracing metadata.
/// </summary>
public interface IExecutionContext : IDictionary<string, string?>, ICloneable
{
}

/// <summary>
/// Default implementation of <see cref="IExecutionContext"/>.
/// </summary>
public sealed class ExecutionContext : Dictionary<string, string?>, IExecutionContext
{
    public ExecutionContext() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public object Clone()
    {
        var copy = new ExecutionContext();
        foreach (var kvp in this)
        {
            copy[kvp.Key] = kvp.Value;
        }
        return copy;
    }
}

public static partial class NOFContractExtensions
{
    extension(IExecutionContext context)
    {
        /// <summary>
        /// The tenant ID of the current execution context.
        /// </summary>
        public string TenantId
        {
            get
            {
                context.TryGetValue(NOFContractConstants.Transport.Headers.TenantId, out var tenantId);
                return NOFContractConstants.Tenant.NormalizeTenantId(tenantId);
            }
            set
            {
                context[NOFContractConstants.Transport.Headers.TenantId] = NOFContractConstants.Tenant.NormalizeTenantId(value);
            }
        }

        /// <summary>
        /// Gets the current tracing information.
        /// </summary>
        public TracingInfo? TracingInfo
        {
            get
            {
                context.TryGetValue(NOFContractConstants.Transport.Headers.TraceId, out var traceId);
                context.TryGetValue(NOFContractConstants.Transport.Headers.SpanId, out var spanId);
                if (traceId is not null && spanId is not null)
                {
                    return new TracingInfo(traceId, spanId);
                }
                return null;
            }
        }

        /// <summary>
        /// Sets the tenant ID of the current execution context.
        /// </summary>
        public void SetTenantId(string tenantId)
        {
            context.TenantId = tenantId;
        }

        /// <summary>
        /// Sets the tracing information in the execution context headers.
        /// </summary>
        public void SetTracingInfo(TracingInfo tracingInfo)
        {
            context[NOFContractConstants.Transport.Headers.TraceId] = tracingInfo.TraceId;
            context[NOFContractConstants.Transport.Headers.SpanId] = tracingInfo.SpanId;
        }

        /// <summary>
        /// Creates a new Activity with the current tracing information as the parent.
        /// </summary>
        public Activity? CreateChildActivity(string name, ActivityKind kind, ActivitySource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            context.TryGetValue(NOFContractConstants.Transport.Headers.TraceId, out var traceId);
            context.TryGetValue(NOFContractConstants.Transport.Headers.SpanId, out var spanId);

            Activity? activity;

            if (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
            {
                var activityId = ActivityTraceId.CreateFromString(traceId.AsSpan());
                var parentSpanId = ActivitySpanId.CreateFromString(spanId.AsSpan());
                var activityContext = new ActivityContext(activityId, parentSpanId, ActivityTraceFlags.Recorded);
                activity = source.CreateActivity(name, kind, parentContext: activityContext);
            }
            else
            {
                activity = source.CreateActivity(name, kind);
                if (!string.IsNullOrEmpty(traceId))
                {
                    activity?.SetParentId(traceId);
                }
            }

            if (activity is not null)
            {
                context.SetTracingInfo(new TracingInfo(activity.TraceId.ToString(), activity.SpanId.ToString()));
                activity.Start();
            }

            return activity;
        }
    }
}

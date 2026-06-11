using NOF.Abstraction;
using System.Diagnostics;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

public static partial class ContextTracingExtensions
{
    extension(Context context)
    {
        public TracingInfo? TracingInfo
            => context.TryGetItem(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId)
                && traceId is string traceIdValue
                && context.TryGetItem(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId)
                && spanId is string spanIdValue
                ? new TracingInfo(traceIdValue, spanIdValue)
                : null;

        public Context WithTracingInfo(TracingInfo? value)
            => value is null
                ? context
                    .WithoutItem(NOFAbstractionConstants.Transport.Headers.TraceId)
                    .WithoutItem(NOFAbstractionConstants.Transport.Headers.SpanId)
                : context
                    .WithItem(NOFAbstractionConstants.Transport.Headers.TraceId, value.TraceId)
                    .WithItem(NOFAbstractionConstants.Transport.Headers.SpanId, value.SpanId);

        public Activity? StartChildActivity(string name, ActivityKind kind, ActivitySource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            Activity? activity;
            var parent = context.TracingInfo;
            if (parent is not null &&
                !string.IsNullOrEmpty(parent.TraceId) &&
                !string.IsNullOrEmpty(parent.SpanId))
            {
                var activityId = ActivityTraceId.CreateFromString(parent.TraceId.AsSpan());
                var parentSpanId = ActivitySpanId.CreateFromString(parent.SpanId.AsSpan());
                var parentContext = new ActivityContext(activityId, parentSpanId, ActivityTraceFlags.Recorded, isRemote: true);
                activity = source.StartActivity(name, kind, parentContext);
            }
            else
            {
                var randomParent = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
                activity = source.StartActivity(name, kind, randomParent);
            }

            return activity;
        }
    }
}

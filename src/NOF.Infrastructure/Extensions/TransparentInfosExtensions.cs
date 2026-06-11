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
            => context.TryGetHeader(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId)
                && context.TryGetHeader(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId)
                && traceId is not null
                && spanId is not null
                ? new TracingInfo(traceId, spanId)
                : null;

        public Context WithTracingInfo(TracingInfo? value)
            => value is null
                ? context
                    .WithoutHeader(NOFAbstractionConstants.Transport.Headers.TraceId)
                    .WithoutHeader(NOFAbstractionConstants.Transport.Headers.SpanId)
                : context
                    .WithHeader(NOFAbstractionConstants.Transport.Headers.TraceId, value.TraceId)
                    .WithHeader(NOFAbstractionConstants.Transport.Headers.SpanId, value.SpanId);

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

            if (activity is not null)
            {
                context = context.WithTracingInfo(new TracingInfo(activity.TraceId.ToString(), activity.SpanId.ToString()));
            }
            return activity;
        }
    }
}

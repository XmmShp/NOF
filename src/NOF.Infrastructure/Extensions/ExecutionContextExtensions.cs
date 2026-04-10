using NOF.Abstraction;
using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure;

public static partial class ExecutionContextExtensions
{
    extension(IExecutionContext context)
    {
        public TracingInfo? TracingInfo
        {
            get
            {
                context.TryGetValue(NOFAbstractionConstants.Transport.Headers.TraceId, out var traceId);
                context.TryGetValue(NOFAbstractionConstants.Transport.Headers.SpanId, out var spanId);
                return (traceId is not null && spanId is not null) ? new TracingInfo(traceId, spanId) : null;
            }
            set
            {
                if (value is null)
                {
                    context.Remove(NOFAbstractionConstants.Transport.Headers.TraceId);
                    context.Remove(NOFAbstractionConstants.Transport.Headers.SpanId);
                }
                else
                {
                    context[NOFAbstractionConstants.Transport.Headers.TraceId] = value.TraceId;
                    context[NOFAbstractionConstants.Transport.Headers.SpanId] = value.SpanId;
                }
            }
        }

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
                context.TracingInfo = new TracingInfo(activity.TraceId.ToString(), activity.SpanId.ToString());
            }
            return activity;
        }
    }
}

using System.Diagnostics;

namespace NOF.Contract;

public static class ActivityExtensions
{
    public static Activity? StartActivityWithParent(this ActivitySource source, string name, ActivityKind kind, TracingInfo? parent)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (parent is not null &&
            !string.IsNullOrEmpty(parent.TraceId) &&
            !string.IsNullOrEmpty(parent.SpanId))
        {
            var activityId = ActivityTraceId.CreateFromString(parent.TraceId.AsSpan());
            var parentSpanId = ActivitySpanId.CreateFromString(parent.SpanId.AsSpan());
            var parentContext = new ActivityContext(activityId, parentSpanId, ActivityTraceFlags.Recorded);
            return source.StartActivity(name, kind, parentContext);
        }
        else
        {
            var randomParent = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
            return source.StartActivity(name, kind, randomParent);
        }
    }
}

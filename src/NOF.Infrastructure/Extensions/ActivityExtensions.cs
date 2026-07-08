using Microsoft.Extensions.Hosting;
using NOF.Infrastructure;

namespace System.Diagnostics;

public static class ActivityExtensions
{
    extension(Activity activity)
    {
        public void SetServiceDeploymentTags(IHostEnvironment hostEnvironment)
        {
            ArgumentNullException.ThrowIfNull(activity);
            ArgumentNullException.ThrowIfNull(hostEnvironment);

            activity.SetTag(NOFInfrastructureConstants.Deployment.Tags.ServiceId, hostEnvironment.ServiceId);
            activity.SetTag(NOFInfrastructureConstants.Deployment.Tags.ServiceName, hostEnvironment.ServiceName);
            activity.SetTag(NOFInfrastructureConstants.Deployment.Tags.InstanceId, hostEnvironment.InstanceId);
        }

        public string ToTraceParent()
        {
            ArgumentNullException.ThrowIfNull(activity);

            return $"00-{activity.TraceId}-{activity.SpanId}-{(byte)activity.ActivityTraceFlags:x2}";
        }
    }

    extension(ActivitySource source)
    {
        public Activity? StartActivityWithParent(string name, ActivityKind kind, string? traceParent, IHostEnvironment? hostEnvironment = null)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (!string.IsNullOrWhiteSpace(traceParent) &&
                ActivityContext.TryParse(traceParent, null, out var parentContext))
            {
                var activity = source.StartActivity(name, kind, parentContext);
                if (hostEnvironment is not null)
                {
                    activity?.SetServiceDeploymentTags(hostEnvironment);
                }

                return activity;
            }

            var randomParent = new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded,
                isRemote: true);
            var startedActivity = source.StartActivity(name, kind, randomParent);
            if (hostEnvironment is not null)
            {
                startedActivity?.SetServiceDeploymentTags(hostEnvironment);
            }

            return startedActivity;
        }

    }
}

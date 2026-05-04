using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure;

public static class ActivityExtensions
{
    extension(Activity activity)
    {
        public void SetServiceDeploymentTags(IHostEnvironment hostEnvironment)
        {
            ArgumentNullException.ThrowIfNull(activity);
            ArgumentNullException.ThrowIfNull(hostEnvironment);

            activity.SetTag(NOFInfrastructureConstants.Deployment.Tags.ApplicationId, hostEnvironment.ApplicationId);
            activity.SetTag(NOFInfrastructureConstants.Deployment.Tags.ApplicationName, hostEnvironment.ApplicationName);
            activity.SetTag(NOFInfrastructureConstants.Deployment.Tags.InstanceId, hostEnvironment.InstanceId);
        }
    }

    extension(ActivitySource source)
    {
        public Activity? StartActivityWithParent(string name, ActivityKind kind, TracingInfo? parent, IHostEnvironment? hostEnvironment = null)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (parent is not null &&
                !string.IsNullOrEmpty(parent.TraceId) &&
                !string.IsNullOrEmpty(parent.SpanId))
            {
                var activityId = ActivityTraceId.CreateFromString(parent.TraceId.AsSpan());
                var parentSpanId = ActivitySpanId.CreateFromString(parent.SpanId.AsSpan());
                var parentContext = new ActivityContext(activityId, parentSpanId, ActivityTraceFlags.Recorded);
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

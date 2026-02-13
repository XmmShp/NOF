using MassTransit;
using NOF.Infrastructure.Core;
using System.Diagnostics;

namespace NOF.Infrastructure.MassTransit;

/// <summary>
/// Shared helper for applying outbound headers and tracing context to MassTransit <see cref="SendContext"/>.
/// </summary>
internal static class SendContextExtensions
{
    extension(SendContext context)
    {
        /// <summary>
        /// Copies caller-provided headers and current <see cref="Activity"/> tracing headers
        /// into the <see cref="SendContext"/>.
        /// </summary>
        public void ApplyHeaders(IDictionary<string, string?>? headers)
        {
            if (headers is not null)
            {
                foreach (var header in headers)
                {
                    context.Headers.Set(header.Key, header.Value);
                }
            }

            var activity = Activity.Current;
            if (activity is not null)
            {
                context.Headers.Set(NOFInfrastructureCoreConstants.Transport.Headers.TraceId, activity.TraceId.ToString());
                context.Headers.Set(NOFInfrastructureCoreConstants.Transport.Headers.SpanId, activity.SpanId.ToString());
            }
        }
    }
}

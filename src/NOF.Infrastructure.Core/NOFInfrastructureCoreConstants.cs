using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Central constants for the NOF Infrastructure Core layer.
/// </summary>
public static partial class NOFInfrastructureCoreConstants
{
    /// <summary>
    /// Constants for JWT authentication.
    /// </summary>
    public static class Jwt
    {
        /// <summary>
        /// JWT token type.
        /// </summary>
        public const string TokenType = "Bearer";

        /// <summary>
        /// Default algorithm for JWT signing.
        /// </summary>
        public const string Algorithm = "RS256";

        /// <summary>
        /// Claim types.
        /// </summary>
        public static class ClaimTypes
        {
            public const string JwtId = "jti";
            public const string Subject = "sub";
            public const string TenantId = "tenant_id";
            public const string Issuer = "iss";
            public const string Audience = "aud";
            public const string IssuedAt = "iat";
            public const string ExpiresAt = "exp";
            public const string Role = "role";
            public const string Permission = "permission";
        }
    }

    /// <summary>
    /// Constants for the JWT client.
    /// </summary>
    public static class JwtClient
    {
        /// <summary>
        /// The named HTTP client used for fetching JWKS from the authority.
        /// </summary>
        public const string JwksHttpClientName = "NOF.JwtClient.Jwks";

        /// <summary>
        /// The well-known JWKS endpoint path.
        /// </summary>
        public const string JwksEndpointPath = "/.well-known/jwks.json";
    }

    /// <summary>
    /// Handler pipeline tracing and metrics constants.
    /// </summary>
    public static class InboundPipeline
    {
        /// <summary>
        /// The ActivitySource name.
        /// </summary>
        public const string ActivitySourceName = "NOF.InboundPipeline";

        /// <summary>
        /// The Meter name.
        /// </summary>
        public const string MeterName = "NOF.InboundPipeline";

        /// <summary>
        /// The ActivitySource instance.
        /// </summary>
        public static readonly ActivitySource Source = new(ActivitySourceName);

        /// <summary>
        /// The Meter instance.
        /// </summary>
        public static readonly Meter Meter = new(MeterName);

        /// <summary>
        /// Activity tag names.
        /// </summary>
        public static class Tags
        {
            public const string HandlerType = "handler.type";
            public const string MessageType = "message.type";
            public const string TenantId = "tenant.id";
        }

        /// <summary>
        /// Metric names.
        /// </summary>
        public static class Metrics
        {
            public const string ExecutionCounter = "nof.handler.executions";
            public const string ExecutionDuration = "nof.handler.duration";
            public const string ErrorCounter = "nof.handler.errors";
        }

        /// <summary>
        /// Metric descriptions.
        /// </summary>
        public static class MetricDescriptions
        {
            public const string ExecutionCounter = "Total number of handler executions";
            public const string ExecutionDuration = "Handler execution duration in milliseconds";
            public const string ErrorCounter = "Total number of handler execution errors";
        }

        /// <summary>
        /// Metric units.
        /// </summary>
        public static class MetricUnits
        {
            public const string Milliseconds = "ms";
        }
    }

    /// <summary>
    /// Message tracing constants.
    /// </summary>
    public static class Messaging
    {
        /// <summary>
        /// The ActivitySource name.
        /// </summary>
        public const string ActivitySourceName = "NOF.Messaging";

        /// <summary>
        /// The ActivitySource instance.
        /// </summary>
        public static readonly ActivitySource Source = new(ActivitySourceName);

        /// <summary>
        /// Activity tag names.
        /// </summary>
        public static class Tags
        {
            public const string MessageId = "messaging.message_id";
            public const string MessageType = "messaging.message_type";
            public const string Destination = "messaging.destination";
            public const string TenantId = "messaging.tenant_id";
        }

        /// <summary>
        /// Activity names.
        /// </summary>
        public static class ActivityNames
        {
            public const string MessageSending = "MessageSending";
        }
    }
}

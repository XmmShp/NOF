namespace NOF.Infrastructure;

/// <summary>
/// Represents distributed tracing information (trace ID and span ID).
/// </summary>
public record TracingInfo(string TraceId, string SpanId);

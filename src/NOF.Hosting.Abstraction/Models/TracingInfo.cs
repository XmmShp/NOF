namespace NOF.Hosting;

/// <summary>
/// Represents distributed tracing information (trace ID and span ID).
/// </summary>
/// <param name="TraceId">The trace ID for distributed tracing across service boundaries.</param>
/// <param name="SpanId">The span ID for the current operation within the trace.</param>
public record TracingInfo(string TraceId, string SpanId);

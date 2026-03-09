namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Executes the inbound handler middleware pipeline.
/// The caller (adapter) constructs the <see cref="InboundContext"/> and provides
/// the terminal handler delegate. The executor wraps it with the middleware pipeline.
/// </summary>
public interface IInboundPipelineExecutor
{
    /// <summary>
    /// Runs the handler pipeline with the given terminal handler delegate.
    /// Middleware can wrap the handler (e.g., for exception handling, tracing, authorization).
    /// The <paramref name="inbound"/> delegate is called as the terminal step of the pipeline.
    /// </summary>
    ValueTask ExecuteAsync(InboundContext context, InboundDelegate inbound, CancellationToken cancellationToken);
}

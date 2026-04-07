namespace NOF.Contract;

/// <summary>
/// Executes the outbound middleware pipeline for messages being sent out.
/// Mirrors <see cref="IInboundPipelineExecutor"/> for the outbound direction.
/// Middleware instances are resolved from DI (scoped) in the order determined
/// by dependency sorting in <see cref="OutboundPipelineTypes"/>.
/// </summary>
public interface IOutboundPipelineExecutor
{
    /// <summary>
    /// Runs the outbound pipeline with the given terminal dispatch delegate.
    /// Middleware can wrap the dispatch (e.g., for tracing spans).
    /// The <paramref name="dispatch"/> delegate is called as the terminal step of the pipeline.
    /// </summary>
    ValueTask ExecuteAsync(OutboundContext context, OutboundDelegate dispatch, CancellationToken cancellationToken);
}

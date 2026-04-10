namespace NOF.Infrastructure;

/// <summary>
/// Executes the inbound handler middleware pipeline.
/// The caller constructs the <see cref="InboundContext"/> and provides
/// the terminal handler delegate.
/// </summary>
public interface IInboundPipelineExecutor
{
    /// <summary>
    /// Runs the handler pipeline with the given terminal handler delegate.
    /// </summary>
    ValueTask ExecuteAsync(InboundContext context, InboundDelegate inbound, CancellationToken cancellationToken);
}

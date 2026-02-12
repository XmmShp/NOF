namespace NOF.Infrastructure.Core;

/// <summary>
/// Provides transport-level headers for the current invocation scope.
/// Implementations bridge specific hosting transports (HTTP, message bus, etc.)
/// into the handler pipeline's <see cref="HandlerContext.Headers"/>.
/// <para>
/// For HTTP hosting, this is typically implemented using <c>IHttpContextAccessor</c>.
/// For message bus hosting, headers are passed directly via the <see cref="IHandlerExecutor"/> API.
/// </para>
/// </summary>
public interface ITransportHeaderProvider
{
    /// <summary>
    /// Gets the transport-level headers for the current invocation.
    /// Returns an empty dictionary if no transport context is available.
    /// </summary>
    IReadOnlyDictionary<string, string?> GetHeaders();
}

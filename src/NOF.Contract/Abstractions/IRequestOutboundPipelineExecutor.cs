using System.ComponentModel;

namespace NOF.Contract;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask RequestOutboundHandlerDelegate(
    RequestOutboundContext context,
    object request,
    CancellationToken cancellationToken);

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestOutboundPipelineExecutor
{
    ValueTask ExecuteAsync(
        RequestOutboundContext context,
        object request,
        RequestOutboundHandlerDelegate dispatch,
        CancellationToken cancellationToken);
}

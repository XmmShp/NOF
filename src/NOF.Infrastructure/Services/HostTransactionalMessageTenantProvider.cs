namespace NOF.Infrastructure;

internal sealed class HostTransactionalMessageTenantProvider : ITransactionalMessageTenantProvider
{
    public async IAsyncEnumerable<string> GetTenantIdsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

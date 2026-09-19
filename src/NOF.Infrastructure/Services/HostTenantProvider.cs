namespace NOF.Infrastructure;

/// <summary>Uses only the host tenant, which infrastructure consumers include automatically.</summary>
public sealed class HostTenantProvider : ITenantProvider
{
    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetTenantIdsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

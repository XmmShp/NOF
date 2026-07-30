using Microsoft.Extensions.Hosting;
using NOF.Application;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OutboxOrderSequenceAllocator(
    IDbContext dbContext,
    IHostEnvironment hostEnvironment)
{
    internal const int MaxOrderKeyLength = 512;

    private readonly Dictionary<string, LocalOrderState> _localStates = new(StringComparer.Ordinal);

    internal async ValueTask<OutboxOrder> AllocateAsync(
        string orderKey,
        bool completesOrderKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderKey);

        var qualifiedOrderKey = Qualify(hostEnvironment.ServiceName, orderKey);
        if (!_localStates.TryGetValue(qualifiedOrderKey, out var localState))
        {
            var latest = await dbContext.Set<NOFOutboxOrderState>()
                .AsNoTracking()
                .Where(state => state.OrderKey == qualifiedOrderKey)
                .OrderByDescending(static state => state.Sequence)
                .FirstOrDefaultAsync(cancellationToken);

            if (latest?.CompletesOrderKey == true)
            {
                throw new InvalidOperationException($"Ordered outbox key '{qualifiedOrderKey}' has already been completed.");
            }

            localState = new LocalOrderState(latest?.Sequence ?? 0);
            _localStates.Add(qualifiedOrderKey, localState);
        }

        if (localState.IsCompleted)
        {
            throw new InvalidOperationException($"Ordered outbox key '{qualifiedOrderKey}' has already been completed in the current unit of work.");
        }

        var sequence = checked(localState.LastSequence + 1);
        localState.LastSequence = sequence;
        localState.IsCompleted = completesOrderKey;

        dbContext.Set<NOFOutboxOrderState>().Add(new NOFOutboxOrderState
        {
            OrderKey = qualifiedOrderKey,
            Sequence = sequence,
            CompletesOrderKey = completesOrderKey
        });

        return new OutboxOrder(qualifiedOrderKey, sequence, completesOrderKey);
    }

    internal static string Qualify(string serviceName, string orderKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderKey);

        var normalizedServiceName = serviceName.Trim();
        var normalizedOrderKey = orderKey.Trim();
        var qualified = $"{normalizedServiceName}:{normalizedOrderKey}";
        if (qualified.Length > MaxOrderKeyLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderKey),
                $"The service-qualified order key cannot exceed {MaxOrderKeyLength} characters.");
        }

        return qualified;
    }

    private sealed class LocalOrderState(long lastSequence)
    {
        public long LastSequence { get; set; } = lastSequence;
        public bool IsCompleted { get; set; }
    }
}

internal readonly record struct OutboxOrder(string OrderKey, long Sequence, bool CompletesOrderKey);

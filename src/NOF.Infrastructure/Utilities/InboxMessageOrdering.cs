using NOF.Abstraction;
using NOF.Application;
using System.ComponentModel;
using System.Globalization;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class InboxMessageOrdering
{
    public static InboxMessageOrder? Parse(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var dictionary = headers?.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        if (dictionary is null)
        {
            return null;
        }

        dictionary.TryGetValue(NOFAbstractionConstants.Transport.Headers.OrderKey, out var orderKey);
        dictionary.TryGetValue(NOFAbstractionConstants.Transport.Headers.Sequence, out var sequenceText);
        var hasOrderKey = !string.IsNullOrWhiteSpace(orderKey);
        var hasSequence = !string.IsNullOrWhiteSpace(sequenceText);
        if (!hasOrderKey && !hasSequence)
        {
            return null;
        }

        if (!hasOrderKey || !hasSequence ||
            !long.TryParse(sequenceText, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) ||
            sequence <= 0)
        {
            throw new InvalidOperationException(
                "Ordered transport messages must contain both a non-empty order key and a positive sequence.");
        }

        var completesOrderKey = dictionary.TryGetValue(
                NOFAbstractionConstants.Transport.Headers.CompletesOrderKey,
                out var completesText) &&
            bool.TryParse(completesText, out var completes) &&
            completes;

        return new InboxMessageOrder(orderKey!.Trim(), sequence, completesOrderKey);
    }

    public static async Task EnsureStateAsync(
        IDbContext dbContext,
        Guid messageId,
        string route,
        InboxMessageOrder? order,
        CancellationToken cancellationToken)
    {
        if (order is null)
        {
            return;
        }

        var value = order.Value;
        var state = await dbContext.Set<NOFInboxOrderState>()
            .FirstOrDefaultAsync(
                candidate => candidate.Route == route && candidate.OrderKey == value.OrderKey,
                cancellationToken);
        if (state is null)
        {
            dbContext.Set<NOFInboxOrderState>().Add(new NOFInboxOrderState
            {
                Route = route,
                OrderKey = value.OrderKey
            });
            return;
        }

        if (value.Sequence != 1 || state.CompletedAtUtc is null)
        {
            return;
        }

        var isDuplicate = await dbContext.Set<NOFInboxMessage>()
            .AsNoTracking()
            .Where(message => message.Id == messageId && message.Route == route)
            .AnyAsync(cancellationToken);
        if (isDuplicate)
        {
            return;
        }

        state.NextSequence = 1;
        state.ClaimedBy = null;
        state.ClaimExpiresAtUtc = null;
        state.UpdatedAtUtc = DateTime.UtcNow;
        state.CompletedAtUtc = null;
        state.BlockedSequence = null;
        state.ErrorMessage = null;
    }
}

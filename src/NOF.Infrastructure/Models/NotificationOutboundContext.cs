using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NotificationOutboundContext : Context
{
    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public NotificationOutboundContext()
    {
    }

    public NotificationOutboundContext(Context context)
        : base(context?.Items ?? throw new ArgumentNullException(nameof(context)))
    {
    }

    private NotificationOutboundContext(IReadOnlyDictionary<object, object?> items, NotificationOutboundContext source)
        : base(items)
    {
        Headers = new Dictionary<string, string?>(source.Headers, StringComparer.OrdinalIgnoreCase);
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new NotificationOutboundContext(items, this);
}

using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CommandOutboundContext : Context
{
    public IDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public CommandOutboundContext()
    {
    }

    public CommandOutboundContext(Context context)
        : base(context?.Items ?? throw new ArgumentNullException(nameof(context)))
    {
        TenantId = context.TenantId;
    }

    private CommandOutboundContext(IReadOnlyDictionary<object, object?> items, CommandOutboundContext source)
        : base(items)
    {
        Headers = new Dictionary<string, string?>(source.Headers, StringComparer.OrdinalIgnoreCase);
        TenantId = source.TenantId;
    }

    protected override Context Clone(IReadOnlyDictionary<object, object?> items)
        => new CommandOutboundContext(items, this);
}

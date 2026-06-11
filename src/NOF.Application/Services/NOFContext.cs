using NOF.Contract;

namespace NOF.Application;

public interface IContextAccessor
{
    Context Context { get; set; }
}

public sealed class ContextAccessor : IContextAccessor
{
    private static readonly AsyncLocal<ContextHolder?> CurrentContext = new();

    public Context Context
    {
        get => CurrentContext.Value?.Context ?? Context.Empty;
        set
        {
            var holder = CurrentContext.Value;
            if (holder is not null)
            {
                holder.Context = null;
            }

            CurrentContext.Value = new ContextHolder
            {
                Context = value
            };
        }
    }

    private sealed class ContextHolder
    {
        public Context? Context { get; set; }
    }
}

public static class AmbientContext
{
    public static IDisposable PushCurrent(IContextAccessor accessor, Context context)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(context);

        var previous = accessor.Context;
        accessor.Context = context;
        return new AmbientContextScope(accessor, previous);
    }

    private sealed class AmbientContextScope(IContextAccessor accessor, Context previousContext) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            accessor.Context = previousContext;
            _disposed = true;
        }
    }
}

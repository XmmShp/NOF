namespace NOF.Contract;

public static partial class ContextExtensions
{
    extension(Context context)
    {
        public Context CopyHeadersFrom(IEnumerable<KeyValuePair<string, string?>>? headers)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (headers is null)
            {
                return context;
            }

            var items = new Dictionary<object, object?>(context.Items);
            foreach (var (headerKey, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(headerKey))
                {
                    continue;
                }

                items[headerKey] = value;
            }

            return context.WithItems(items);
        }

        public Context ReplaceHeadersFrom(IEnumerable<KeyValuePair<string, string?>>? headers)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (headers is null)
            {
                return Context.Empty;
            }

            var items = new Dictionary<object, object?>();
            foreach (var (headerKey, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(headerKey))
                {
                    continue;
                }

                items[headerKey] = value;
            }

            return Context.Empty.WithItems(items);
        }
    }
}

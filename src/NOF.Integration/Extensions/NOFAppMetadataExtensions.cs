namespace NOF;

public static partial class __NOF_Infrastructure_Extensions__
{
    extension(INOFAppMetadata metadata)
    {
        public T GetOrAdd<T>(string name, Func<string, T> valueFactory)
        {
            if (metadata.TryGet<T>(name, out var value))
            {
                return value;
            }

            var defaultValue = valueFactory(name);
            metadata.Set(name, defaultValue);
            return defaultValue;
        }

        public T? GetOrDefault<T>(string name, T? defaultValue = default)
            => metadata.TryGet<T>(name, out var value) ? value : defaultValue;

        public T GetOrDefault<T>(string name, Func<string, T> defaultValueFactory)
            => metadata.TryGet<T>(name, out var value) ? value : defaultValueFactory(name);
    }
}

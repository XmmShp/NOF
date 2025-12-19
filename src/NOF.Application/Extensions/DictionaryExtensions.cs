namespace NOF;

public static partial class __NOF_Application_Extensions__
{
    extension<TKey, T>(IDictionary<TKey, List<T>> dict) where TKey : notnull
    {
        public List<T> GetOrAdd(TKey key)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = [];
                dict[key] = list;
            }
            return list;
        }
    }
}

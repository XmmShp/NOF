namespace System.Collections.Generic;

public static class DictionaryExtensions
{
    extension(IDictionary<string, string>? dictionary)
    {
        public Dictionary<string, string> CreateOrCopy()
        {
            return dictionary is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(dictionary);
        }
    }
}

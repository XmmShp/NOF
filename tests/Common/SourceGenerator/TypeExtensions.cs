using Microsoft.CodeAnalysis;

namespace System;

public static class TypeExtensions
{
    extension(Type type)
    {
        public MetadataReference ToMetadataReference()
        {
            return MetadataReference.CreateFromFile(type.Assembly.Location);
        }
    }
}

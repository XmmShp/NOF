using Microsoft.CodeAnalysis;

namespace NOF.SourceGenerator.Tests.Extensions;

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

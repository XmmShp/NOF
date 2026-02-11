using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NOF.Contract;

namespace NOF.Hosting.AspNetCore;

internal class OptionalSchemaTransformer : IOpenApiSchemaTransformer
{
    public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Optional<>))
        {
            return;
        }

        var innerType = type.GetGenericArguments().First();
        var innerSchema = await context.GetOrCreateSchemaAsync(innerType, null, CancellationToken.None);

        // Replace the Optional<T> schema with the inner type schema
        schema.Type = innerSchema.Type;
        schema.Format = innerSchema.Format;
        schema.Properties = innerSchema.Properties;
        schema.Items = innerSchema.Items;
        schema.AdditionalProperties = innerSchema.AdditionalProperties;
        schema.AllOf = innerSchema.AllOf;
        schema.AnyOf = innerSchema.AnyOf;
        schema.OneOf = innerSchema.OneOf;
        schema.Enum = innerSchema.Enum;
        schema.Minimum = innerSchema.Minimum;
        schema.Maximum = innerSchema.Maximum;
        schema.Pattern = innerSchema.Pattern;
        schema.MinLength = innerSchema.MinLength;
        schema.MaxLength = innerSchema.MaxLength;
    }
}

using NOF.Infrastructure;

namespace Microsoft.EntityFrameworkCore.Metadata.Builders;

public static class EntityTypeBuilderExtensions
{
    extension<TEntity>(EntityTypeBuilder<TEntity> entityBuilder) where TEntity : class
    {
        public EntityTypeBuilder<TEntity> IsHostOnly()
        {
            ArgumentNullException.ThrowIfNull(entityBuilder);
            entityBuilder.Metadata.SetAnnotation(TenantModelHelper.HostOnlyAnnotationName, true);
            return entityBuilder;
        }
    }

    extension(EntityTypeBuilder entityBuilder)
    {
        public EntityTypeBuilder IsHostOnly()
        {
            ArgumentNullException.ThrowIfNull(entityBuilder);
            entityBuilder.Metadata.SetAnnotation(TenantModelHelper.HostOnlyAnnotationName, true);
            return entityBuilder;
        }
    }
}

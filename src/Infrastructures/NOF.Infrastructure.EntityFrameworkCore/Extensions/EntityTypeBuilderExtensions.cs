using NOF.Infrastructure.EntityFrameworkCore;

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

        public EntityTypeBuilder<TEntity> HasSoftDelete(bool enabled = true)
        {
            ArgumentNullException.ThrowIfNull(entityBuilder);
            entityBuilder.Metadata.SetAnnotation(SoftDeleteModelHelper.SoftDeleteEnabledAnnotationName, enabled);
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

        public EntityTypeBuilder HasSoftDelete(bool enabled = true)
        {
            ArgumentNullException.ThrowIfNull(entityBuilder);
            entityBuilder.Metadata.SetAnnotation(SoftDeleteModelHelper.SoftDeleteEnabledAnnotationName, enabled);
            return entityBuilder;
        }
    }
}

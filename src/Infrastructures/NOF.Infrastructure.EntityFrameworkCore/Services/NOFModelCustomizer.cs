using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class NOFModelCustomizer : ModelCustomizer
{
    public NOFModelCustomizer(ModelCustomizerDependencies dependencies) : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        if (context is not NOFDbContext dbContext)
        {
            return;
        }

        var entityTypes = modelBuilder.Model.GetEntityTypes().ToList();
        var useTenantDiscriminator = dbContext.CurrentTenantMode == TenantMode.SharedDatabase;
        var useSoftDelete = dbContext.CurrentSoftDeleteEnabled;

        foreach (var entityType in entityTypes)
        {
            if (entityType.ClrType is null)
            {
                continue;
            }

            var shouldConfigureSoftDelete = useSoftDelete && SoftDeleteModelHelper.ShouldConfigureSoftDelete(entityType);
            var shouldConfigureTenantBehavior = TenantModelHelper.ShouldConfigureTenantBehavior(entityType);
            if (!shouldConfigureSoftDelete && !shouldConfigureTenantBehavior)
            {
                continue;
            }

            var entityBuilder = modelBuilder.Entity(entityType.ClrType);

            if (shouldConfigureSoftDelete)
            {
                entityBuilder.Property<long>(SoftDeleteModelHelper.DeletedAtUnixTimePropertyName)
                    .HasDefaultValue(SoftDeleteModelHelper.ActiveDeletedAtUnixTime);
                entityBuilder.HasIndex(SoftDeleteModelHelper.DeletedAtUnixTimePropertyName);
                entityBuilder.HasQueryFilter(
                    SoftDeleteModelHelper.DeletedAtUnixTimePropertyName,
                    SoftDeleteModelHelper.BuildSoftDeleteFilter(entityType.ClrType));
            }

            if (!shouldConfigureTenantBehavior)
            {
                if (shouldConfigureSoftDelete)
                {
                    ConfigureSoftDeleteUniqueIndexes(entityType, entityBuilder);
                }

                continue;
            }

            var isHostOnly = TenantModelHelper.IsHostOnlyEntity(entityType);
            if (!useTenantDiscriminator)
            {
                if (isHostOnly)
                {
                    entityType.SetAnnotation(TenantModelHelper.HostOnlyAnnotationName, true);
                }
                else
                {
                    entityType.RemoveAnnotation(TenantModelHelper.HostOnlyAnnotationName);
                }
                if (shouldConfigureSoftDelete)
                {
                    ConfigureSoftDeleteUniqueIndexes(entityType, entityBuilder);
                }

                continue;
            }

            if (isHostOnly)
            {
                entityType.SetAnnotation(TenantModelHelper.HostOnlyAnnotationName, true);
                if (shouldConfigureSoftDelete)
                {
                    ConfigureSoftDeleteUniqueIndexes(entityType, entityBuilder);
                }

                continue;
            }

            entityType.RemoveAnnotation(TenantModelHelper.HostOnlyAnnotationName);

            var tenantProperty = entityBuilder.Property<string>(TenantModelHelper.TenantIdPropertyName);
            tenantProperty.HasMaxLength(TenantModelHelper.TenantIdMaxLength);
            tenantProperty.IsRequired();
            tenantProperty.IsConcurrencyToken();

            entityBuilder.HasIndex(TenantModelHelper.TenantIdPropertyName);
            ConfigureUniqueIndexes(entityType, entityBuilder);

            entityBuilder.HasQueryFilter(
                TenantModelHelper.TenantIdPropertyName,
                TenantModelHelper.BuildTenantFilter(entityType.ClrType, dbContext));

            if (shouldConfigureSoftDelete)
            {
                ConfigureSoftDeleteUniqueIndexes(entityType, entityBuilder);
            }
        }
    }

    private static void ConfigureUniqueIndexes(IMutableEntityType entityType, EntityTypeBuilder entityBuilder)
    {
        var uniqueIndexes = entityType.GetIndexes()
            .Where(index => index.IsUnique
                && index.Properties.All(property => property.Name != TenantModelHelper.TenantIdPropertyName))
            .ToList();

        foreach (var index in uniqueIndexes)
        {
            index.IsUnique = false;

            entityBuilder.HasIndex(
                    [TenantModelHelper.TenantIdPropertyName, .. index.Properties.Select(property => property.Name)])
                .IsUnique();
        }
    }

    private static void ConfigureSoftDeleteUniqueIndexes(IMutableEntityType entityType, EntityTypeBuilder entityBuilder)
    {
        var uniqueIndexes = entityType.GetIndexes()
            .Where(index => index.IsUnique
                && index.Properties.All(property => property.Name != SoftDeleteModelHelper.DeletedAtUnixTimePropertyName))
            .ToList();

        foreach (var index in uniqueIndexes)
        {
            index.IsUnique = false;

            entityBuilder.HasIndex(
                    [.. index.Properties.Select(property => property.Name), SoftDeleteModelHelper.DeletedAtUnixTimePropertyName])
                .IsUnique();
        }
    }
}

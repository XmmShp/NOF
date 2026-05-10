using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOF.Application;

namespace NOF.Infrastructure;

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
                entityBuilder.Property<DateTime?>(SoftDeleteModelHelper.DeletedAtUtcPropertyName);
                entityBuilder.HasIndex(SoftDeleteModelHelper.DeletedAtUtcPropertyName);
                entityBuilder.HasQueryFilter(
                    SoftDeleteModelHelper.DeletedAtUtcPropertyName,
                    SoftDeleteModelHelper.BuildSoftDeleteFilter(entityType.ClrType));
            }

            if (!shouldConfigureTenantBehavior)
            {
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
                continue;
            }

            if (isHostOnly)
            {
                entityType.SetAnnotation(TenantModelHelper.HostOnlyAnnotationName, true);
                continue;
            }

            entityType.RemoveAnnotation(TenantModelHelper.HostOnlyAnnotationName);

            var tenantProperty = entityBuilder.Property<string>(TenantModelHelper.TenantIdPropertyName);
            tenantProperty.HasMaxLength(TenantModelHelper.TenantIdMaxLength);
            tenantProperty.IsRequired();
            tenantProperty.IsConcurrencyToken();

            entityBuilder.HasIndex(TenantModelHelper.TenantIdPropertyName);
            ConfigureUniqueIndexes(entityType, entityBuilder);

            if (entityType.ClrType == typeof(NOFStateMachineContext))
            {
                entityBuilder.HasKey(
                    nameof(NOFStateMachineContext.CorrelationId),
                    nameof(NOFStateMachineContext.DefinitionTypeName),
                    TenantModelHelper.TenantIdPropertyName);
            }

            entityBuilder.HasQueryFilter(
                TenantModelHelper.TenantIdPropertyName,
                TenantModelHelper.BuildTenantFilter(entityType.ClrType, dbContext));
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
}

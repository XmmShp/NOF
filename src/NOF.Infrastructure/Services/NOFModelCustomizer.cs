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

        var hostOnlyTypes = TenantModelHelper.CreateHostOnlyTypeSet(dbContext);
        var entityTypes = modelBuilder.Model.GetEntityTypes().ToList();
        var useTenantDiscriminator = dbContext.CurrentTenantMode == TenantMode.SharedDatabase;

        foreach (var entityType in entityTypes)
        {
            if (!TenantModelHelper.ShouldConfigureTenantBehavior(entityType)
                || entityType.ClrType is null)
            {
                continue;
            }

            var entityBuilder = modelBuilder.Entity(entityType.ClrType);
            if (!useTenantDiscriminator)
            {
                entityType.RemoveAnnotation(TenantModelHelper.TenantScopedAnnotationName);
                entityType.RemoveAnnotation(TenantModelHelper.HostOnlyAnnotationName);
                continue;
            }

            if (TenantModelHelper.IsHostOnlyType(entityType.ClrType, hostOnlyTypes))
            {
                entityType.SetAnnotation(TenantModelHelper.HostOnlyAnnotationName, true);
                entityType.RemoveAnnotation(TenantModelHelper.TenantScopedAnnotationName);
                continue;
            }

            entityType.SetAnnotation(TenantModelHelper.TenantScopedAnnotationName, true);
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
                TenantModelHelper.TenantScopedAnnotationName,
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

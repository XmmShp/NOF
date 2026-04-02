using Microsoft.EntityFrameworkCore;
using NOF.Application;

namespace NOF.Infrastructure.EntityFrameworkCore;

public abstract class NOFDbContext : DbContext
{
    private readonly NOFTenantDbContextOptionsExtension _tenantOptions;

    protected NOFDbContext(DbContextOptions options) : base(options)
    {
        _tenantOptions = options.FindExtension<NOFTenantDbContextOptionsExtension>() ?? new NOFTenantDbContextOptionsExtension();
    }

    public string? CurrentTenantId => _tenantOptions.TenantId;
    public string CurrentTenantKey => CurrentTenantId ?? string.Empty;

    internal DbSet<NOFStateMachineContext> NOFStateMachineContexts { get; set; }
    internal DbSet<NOFInboxMessage> NOFInboxMessages { get; set; }
    internal DbSet<NOFOutboxMessage> NOFOutboxMessages { get; set; }
    internal DbSet<NOFTenant> NOFTenants { get; set; }

    protected virtual Type[] GetHostOnlyEntityTypes() => [typeof(NOFTenant), typeof(NOFInboxMessage), typeof(NOFOutboxMessage)];

    internal Type[] GetHostOnlyEntityTypesInternal() => GetHostOnlyEntityTypes();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NOFTenant>(entity =>
        {
            entity.ToTable(nameof(NOFTenant));
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<NOFInboxMessage>(entity =>
        {
            entity.ToTable(nameof(NOFInboxMessage));
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<NOFOutboxMessage>(entity =>
        {
            entity.ToTable(nameof(NOFOutboxMessage));
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.ClaimExpiresAt });
            entity.HasIndex(e => e.ClaimedBy);
            entity.HasIndex(e => e.TraceId);
            entity.Property(e => e.PayloadType).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Headers).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2048);
            entity.Property(e => e.ClaimedBy).HasMaxLength(256);
            entity.Property(e => e.TraceId).HasMaxLength(128);
            entity.Property(e => e.SpanId).HasMaxLength(128);
        });

        modelBuilder.Entity<NOFStateMachineContext>(entity =>
        {
            entity.ToTable(nameof(NOFStateMachineContext));
            entity.HasKey(e => new { e.CorrelationId, e.DefinitionTypeName });
            entity.Property(e => e.CorrelationId).IsRequired();
            entity.Property(e => e.DefinitionTypeName).IsRequired();
        });
    }

    public override object? Find(Type entityType, params object?[]? keyValues)
        => base.Find(entityType, AppendTenantKeyIfNeeded(entityType, keyValues));

    public override TEntity? Find<TEntity>(params object?[]? keyValues)
        where TEntity : class
        => base.Find<TEntity>(AppendTenantKeyIfNeeded(typeof(TEntity), keyValues));

    public override ValueTask<object?> FindAsync(Type entityType, params object?[]? keyValues)
        => base.FindAsync(entityType, AppendTenantKeyIfNeeded(entityType, keyValues));

    public override ValueTask<object?> FindAsync(Type entityType, object?[]? keyValues, CancellationToken cancellationToken)
        => base.FindAsync(entityType, AppendTenantKeyIfNeeded(entityType, keyValues), cancellationToken);

    public override ValueTask<TEntity?> FindAsync<TEntity>(params object?[]? keyValues)
        where TEntity : class
        => base.FindAsync<TEntity>(AppendTenantKeyIfNeeded(typeof(TEntity), keyValues));

    public override ValueTask<TEntity?> FindAsync<TEntity>(object?[]? keyValues, CancellationToken cancellationToken)
        where TEntity : class
        => base.FindAsync<TEntity>(AppendTenantKeyIfNeeded(typeof(TEntity), keyValues), cancellationToken);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTenantRules()
    {
        var hostOnlyTypes = TenantModelHelper.CreateHostOnlyTypeSet(this);

        foreach (var entry in ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Metadata.IsOwned())
            {
                continue;
            }

            if (entry.Metadata.ClrType is not null
                && TenantModelHelper.IsHostOnlyType(entry.Metadata.ClrType, hostOnlyTypes))
            {
                continue;
            }

            var tenantProperty = entry.Properties.FirstOrDefault(property => property.Metadata.Name == TenantModelHelper.TenantIdPropertyName);
            if (tenantProperty is null)
            {
                continue;
            }

            tenantProperty.OriginalValue = CurrentTenantKey;

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                tenantProperty.CurrentValue = CurrentTenantKey;
            }
        }
    }

    private object?[]? AppendTenantKeyIfNeeded(Type entityClrType, object?[]? keyValues)
    {
        if (keyValues is null)
        {
            return null;
        }

        var entityType = Model.FindEntityType(entityClrType);
        var primaryKey = entityType?.FindPrimaryKey();
        if (primaryKey is null
            || primaryKey.Properties.Count != keyValues.Length + 1
            || primaryKey.Properties[^1].Name != TenantModelHelper.TenantIdPropertyName)
        {
            return keyValues;
        }

        return [.. keyValues, CurrentTenantKey];
    }
}

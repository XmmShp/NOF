using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NOF.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure.EntityFrameworkCore;

public class NOFDbContext : DbContext
{
    private static long _lastDeletedAtUnixTime;

    private readonly NOFTenantDbContextOptionsExtension _tenantOptions;
    private readonly NOFModelCreatingDbContextOptionsExtension _modelCreatingOptions;

    public NOFDbContext(DbContextOptions options) : base(options)
    {
        _tenantOptions = options.FindExtension<NOFTenantDbContextOptionsExtension>() ?? new NOFTenantDbContextOptionsExtension();
        _modelCreatingOptions = options.FindExtension<NOFModelCreatingDbContextOptionsExtension>() ?? new NOFModelCreatingDbContextOptionsExtension();
    }

    public string CurrentTenantId => _tenantOptions.TenantId;
    public TenantMode CurrentTenantMode => _tenantOptions.TenantMode;
    public bool CurrentSoftDeleteEnabled => _tenantOptions.SoftDeleteEnabled;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        _modelCreatingOptions.ApplyModelCreating(modelBuilder);
    }

    public override object? Find(Type entityType, params object?[]? keyValues)
        => HideSoftDeletedEntity(base.Find(entityType, AppendTenantKeyIfNeeded(entityType, keyValues)));

    public override TEntity? Find<TEntity>(params object?[]? keyValues)
        where TEntity : class
        => HideSoftDeletedEntity(base.Find<TEntity>(AppendTenantKeyIfNeeded(typeof(TEntity), keyValues)));

    public override async ValueTask<object?> FindAsync(Type entityType, params object?[]? keyValues)
        => HideSoftDeletedEntity(await base.FindAsync(entityType, AppendTenantKeyIfNeeded(entityType, keyValues)));

    public override async ValueTask<object?> FindAsync(Type entityType, object?[]? keyValues, CancellationToken cancellationToken)
        => HideSoftDeletedEntity(await base.FindAsync(entityType, AppendTenantKeyIfNeeded(entityType, keyValues), cancellationToken));

    public override async ValueTask<TEntity?> FindAsync<TEntity>(params object?[]? keyValues)
        where TEntity : class
        => HideSoftDeletedEntity(await base.FindAsync<TEntity>(AppendTenantKeyIfNeeded(typeof(TEntity), keyValues)));

    public override async ValueTask<TEntity?> FindAsync<TEntity>(object?[]? keyValues, CancellationToken cancellationToken)
        where TEntity : class
        => HideSoftDeletedEntity(await base.FindAsync<TEntity>(AppendTenantKeyIfNeeded(typeof(TEntity), keyValues), cancellationToken));

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplySoftDeleteRules();
        ApplyTenantRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplySoftDeleteRules();
        ApplyTenantRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }



    private void ApplySoftDeleteRules()
    {
        if (!CurrentSoftDeleteEnabled)
        {
            return;
        }

        var deletedEntries = ChangeTracker.Entries()
            .Where(entry => entry.State == EntityState.Deleted)
            .ToList();
        if (deletedEntries.Count == 0)
        {
            return;
        }

        var softDeletedEntries = new HashSet<EntityEntry>();
        foreach (var entry in deletedEntries)
        {
            if (entry.Metadata.FindProperty(SoftDeleteModelHelper.DeletedAtUnixTimePropertyName) is null)
            {
                continue;
            }

            entry.State = EntityState.Unchanged;
            entry.Property(SoftDeleteModelHelper.DeletedAtUnixTimePropertyName).CurrentValue = GetCurrentUnixTimeMicroseconds();
            entry.Property(SoftDeleteModelHelper.DeletedAtUnixTimePropertyName).IsModified = true;
            softDeletedEntries.Add(entry);
        }

        if (softDeletedEntries.Count == 0)
        {
            return;
        }

        var preservedOwnedEntries = new HashSet<EntityEntry>();
        foreach (var entry in softDeletedEntries)
        {
            PreserveOwnedGraph(entry, preservedOwnedEntries);
        }

        PreserveForeignKeysForSoftDeletedPrincipals(softDeletedEntries);

        var pendingOwnedEntries = ChangeTracker.Entries()
            .Where(entry => entry.Metadata.IsOwned()
                && entry.State is EntityState.Deleted or EntityState.Modified)
            .ToList();

        var madeProgress = true;
        while (madeProgress && pendingOwnedEntries.Count > 0)
        {
            madeProgress = false;

            foreach (var entry in pendingOwnedEntries.ToList())
            {
                var ownerEntry = FindOwnershipPrincipalEntry(entry);
                if (ownerEntry is null
                    || (!softDeletedEntries.Contains(ownerEntry) && !preservedOwnedEntries.Contains(ownerEntry)))
                {
                    continue;
                }

                entry.State = EntityState.Unchanged;
                preservedOwnedEntries.Add(entry);
                pendingOwnedEntries.Remove(entry);
                madeProgress = true;
            }
        }
    }

    private TEntity? HideSoftDeletedEntity<TEntity>(TEntity? entity)
        where TEntity : class
        => IsSoftDeletedEntity(entity) ? null : entity;

    private object? HideSoftDeletedEntity(object? entity)
        => IsSoftDeletedEntity(entity) ? null : entity;

    private bool IsSoftDeletedEntity(object? entity)
    {
        if (entity is null || !CurrentSoftDeleteEnabled)
        {
            return false;
        }

        var entry = Entry(entity);
        if (entry.Metadata.FindProperty(SoftDeleteModelHelper.DeletedAtUnixTimePropertyName) is null)
        {
            return false;
        }

        return entry.Property(SoftDeleteModelHelper.DeletedAtUnixTimePropertyName).CurrentValue is long deletedAtUnixTime
            && deletedAtUnixTime != SoftDeleteModelHelper.ActiveDeletedAtUnixTime;
    }

    private static long GetCurrentUnixTimeMicroseconds()
    {
        var unixTime = Math.Max(
            1,
            (DateTimeOffset.UtcNow.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / 10);

        while (true)
        {
            var lastUnixTime = Volatile.Read(ref _lastDeletedAtUnixTime);
            var nextUnixTime = Math.Max(unixTime, lastUnixTime + 1);
            if (Interlocked.CompareExchange(ref _lastDeletedAtUnixTime, nextUnixTime, lastUnixTime) == lastUnixTime)
            {
                return nextUnixTime;
            }
        }
    }

    private void ApplyTenantRules()
    {
        ValidateTenantEntries();

        if (CurrentTenantMode != TenantMode.SharedDatabase)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Metadata.IsOwned())
            {
                continue;
            }

            if (TenantModelHelper.IsHostOnlyEntity(entry.Metadata))
            {
                continue;
            }

            var tenantProperty = entry.Properties.FirstOrDefault(property => property.Metadata.Name == TenantModelHelper.TenantIdPropertyName);
            if (tenantProperty is null)
            {
                continue;
            }

            tenantProperty.OriginalValue = CurrentTenantId;

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                tenantProperty.CurrentValue = CurrentTenantId;
            }
        }
    }

    private void ValidateTenantEntries()
    {
        foreach (var entry in ChangeTracker.Entries<NOFTenant>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity.Id == TenantId.Host)
            {
                throw new InvalidOperationException(
                    $"Tenant id '{NOFAbstractionConstants.Tenant.HostId}' is reserved for the host tenant and cannot be created or updated as a tenant record.");
            }
        }
    }

    private EntityEntry? FindOwnershipPrincipalEntry(EntityEntry entry)
    {
        var ownership = entry.Metadata.FindOwnership();
        if (ownership is null)
        {
            return null;
        }

        foreach (var candidate in ChangeTracker.Entries()
                     .Where(candidate => candidate.Metadata == ownership.PrincipalEntityType))
        {
            var matches = true;
            for (var index = 0; index < ownership.Properties.Count; index++)
            {
                var dependentProperty = ownership.Properties[index];
                var principalProperty = ownership.PrincipalKey.Properties[index];
                var dependentValue = entry.Property(dependentProperty.Name).CurrentValue
                    ?? entry.Property(dependentProperty.Name).OriginalValue;
                var principalValue = candidate.Property(principalProperty.Name).CurrentValue
                    ?? candidate.Property(principalProperty.Name).OriginalValue;
                if (!Equals(dependentValue, principalValue))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return candidate;
            }
        }

        return null;
    }

    private void PreserveOwnedGraph(EntityEntry ownerEntry, HashSet<EntityEntry> preservedOwnedEntries)
    {
        foreach (var reference in ownerEntry.References)
        {
            if (reference.TargetEntry is null || !reference.TargetEntry.Metadata.IsOwned())
            {
                continue;
            }

            PreserveOwnedEntry(reference.TargetEntry, preservedOwnedEntries);
        }

        foreach (var collection in ownerEntry.Collections)
        {
            if (collection.CurrentValue is null)
            {
                continue;
            }

            foreach (var entity in collection.CurrentValue)
            {
                var entry = Entry(entity);
                if (!entry.Metadata.IsOwned())
                {
                    continue;
                }

                PreserveOwnedEntry(entry, preservedOwnedEntries);
            }
        }
    }

    private void PreserveOwnedEntry(EntityEntry entry, HashSet<EntityEntry> preservedOwnedEntries)
    {
        if (!entry.Metadata.IsOwned())
        {
            return;
        }

        if (entry.State is EntityState.Deleted or EntityState.Modified)
        {
            entry.State = EntityState.Unchanged;
        }

        if (!preservedOwnedEntries.Add(entry))
        {
            return;
        }

        PreserveOwnedGraph(entry, preservedOwnedEntries);
    }

    private void PreserveForeignKeysForSoftDeletedPrincipals(HashSet<EntityEntry> softDeletedEntries)
    {
        foreach (var entry in ChangeTracker.Entries()
                     .Where(entry => entry.State == EntityState.Modified && !entry.Metadata.IsOwned()))
        {
            foreach (var foreignKey in entry.Metadata.GetForeignKeys().Where(foreignKey => !foreignKey.IsOwnership))
            {
                var originalForeignKeyValues = foreignKey.Properties
                    .Select(property => entry.Property(property.Name).OriginalValue)
                    .ToArray();
                var currentForeignKeyValues = foreignKey.Properties
                    .Select(property => entry.Property(property.Name).CurrentValue)
                    .ToArray();
                if (originalForeignKeyValues.All(value => value is null)
                    || originalForeignKeyValues.SequenceEqual(currentForeignKeyValues))
                {
                    continue;
                }

                var softDeletedPrincipal = softDeletedEntries.FirstOrDefault(candidate =>
                    candidate.Metadata == foreignKey.PrincipalEntityType
                    && foreignKey.PrincipalKey.Properties
                        .Select(property => candidate.Property(property.Name).CurrentValue)
                        .SequenceEqual(originalForeignKeyValues));
                if (softDeletedPrincipal is null)
                {
                    continue;
                }

                foreach (var property in foreignKey.Properties)
                {
                    var propertyEntry = entry.Property(property.Name);
                    propertyEntry.CurrentValue = propertyEntry.OriginalValue;
                    propertyEntry.IsModified = false;
                }

                if (entry.Properties.All(property => !property.IsModified))
                {
                    entry.State = EntityState.Unchanged;
                }
            }
        }
    }

    private object?[]? AppendTenantKeyIfNeeded([DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.NonPublicConstructorsWithInherited
        | DynamicallyAccessedMemberTypes.PublicFields
        | DynamicallyAccessedMemberTypes.NonPublicFields
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.NonPublicProperties
        | DynamicallyAccessedMemberTypes.Interfaces)] Type entityClrType, object?[]? keyValues)
    {
        if (CurrentTenantMode != TenantMode.SharedDatabase)
        {
            return keyValues;
        }

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

        return [.. keyValues, CurrentTenantId];
    }
}

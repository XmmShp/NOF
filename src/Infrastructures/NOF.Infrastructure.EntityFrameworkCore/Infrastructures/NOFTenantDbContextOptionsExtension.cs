using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

/// <summary>
/// DbContext options extension added for tenant contexts.
/// When present, it indicates the DbContext is operating in tenant mode:
/// - Host-only entities are ignored in the model
/// - Migration SQL generation filters out host-only table operations
/// </summary>
internal class NOFTenantDbContextOptionsExtension : IDbContextOptionsExtension
{
    public void ApplyServices(IServiceCollection services)
    {
        // Find the existing IMigrationsSqlGenerator registration (added by the database provider)
        var originalDescriptor = services.LastOrDefault(
            d => d.ServiceType == typeof(IMigrationsSqlGenerator));

        if (originalDescriptor?.ImplementationType == null)
            return;

        // Remove the original registration
        services.Remove(originalDescriptor);

        // Re-register with a factory that wraps the original in our filtering decorator
        services.Add(ServiceDescriptor.Describe(
            typeof(IMigrationsSqlGenerator),
            sp =>
            {
                var inner = (IMigrationsSqlGenerator)ActivatorUtilities.CreateInstance(
                    sp, originalDescriptor.ImplementationType);
                return new NOFTenantMigrationsSqlGenerator(inner);
            },
            originalDescriptor.Lifetime));
    }

    public void Validate(IDbContextOptions options) { }

    public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        public ExtensionInfo(IDbContextOptionsExtension extension) : base(extension) { }

        public override bool IsDatabaseProvider => false;
        public override string LogFragment => "TenantContext ";

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;

        public override int GetServiceProviderHashCode() => 0;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["NOF:TenantContext"] = "true";
    }
}

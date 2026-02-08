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
    /// <summary>
    /// The tenant ID for this context. When null or whitespace, the context is considered to be in host mode.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Additional entity types to ignore in tenant mode, beyond those marked with <see cref="HostOnlyAttribute"/>.
    /// </summary>
    public Type[] TenantIgnoredEntityTypes { get; set; } = [];

    public void ApplyServices(IServiceCollection services)
    {
        // Host mode — no filtering needed
        if (string.IsNullOrWhiteSpace(TenantId))
            return;

        // Find the existing IMigrationsSqlGenerator registration (added by the database provider)
        var originalDescriptor = services.LastOrDefault(
            d => d.ServiceType == typeof(IMigrationsSqlGenerator));

        if (originalDescriptor == null)
            return;

        // Remove the original registration
        services.Remove(originalDescriptor);

        // Re-register with a factory that wraps the original in our filtering decorator
        services.Add(ServiceDescriptor.Describe(
            typeof(IMigrationsSqlGenerator),
            sp =>
            {
                var inner = ResolveInner(sp, originalDescriptor);
                return new NOFTenantMigrationsSqlGenerator(inner, TenantIgnoredEntityTypes);
            },
            originalDescriptor.Lifetime));
    }

    private static IMigrationsSqlGenerator ResolveInner(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType is not null)
            return (IMigrationsSqlGenerator)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);

        if (descriptor.ImplementationFactory is not null)
            return (IMigrationsSqlGenerator)descriptor.ImplementationFactory(sp);

        return (IMigrationsSqlGenerator)descriptor.ImplementationInstance!;
    }

    public void Validate(IDbContextOptions options) { }

    public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        public ExtensionInfo(IDbContextOptionsExtension extension) : base(extension) { }

        private new NOFTenantDbContextOptionsExtension Extension
            => (NOFTenantDbContextOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider => false;
        public override string LogFragment => $"TenantContext(TenantId={Extension.TenantId}) ";

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;

        public override int GetServiceProviderHashCode()
            => (Extension.TenantId ?? string.Empty).GetHashCode();

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            debugInfo["NOF:TenantContext"] = "true";
            debugInfo["NOF:TenantId"] = Extension.TenantId ?? "(null)";
        }
    }
}

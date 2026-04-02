using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// DbContext options extension added for tenant-aware contexts.
/// </summary>
internal class NOFTenantDbContextOptionsExtension : IDbContextOptionsExtension
{
    public string? TenantId { get; set; }

    public void ApplyServices(IServiceCollection services)
    {
        var originalDescriptor = services.LastOrDefault(
            descriptor => descriptor.ServiceType == typeof(IMigrationsModelDiffer));

        if (originalDescriptor is null)
        {
            return;
        }

        services.Remove(originalDescriptor);
        services.Add(ServiceDescriptor.Describe(
            typeof(IMigrationsModelDiffer),
            sp =>
            {
                var inner = ResolveInner(sp, originalDescriptor);
                return new NOFTenantMigrationsModelDiffer(inner);
            },
            originalDescriptor.Lifetime));
    }

    private static IMigrationsModelDiffer ResolveInner(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType is not null)
        {
            return (IMigrationsModelDiffer)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IMigrationsModelDiffer)descriptor.ImplementationFactory(sp);
        }

        return (IMigrationsModelDiffer)descriptor.ImplementationInstance!;
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
            => 0;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            debugInfo["NOF:TenantContext"] = "true";
            debugInfo["NOF:TenantId"] = Extension.TenantId ?? "(null)";
        }
    }
}

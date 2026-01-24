using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace NOF;

internal class NOFDbContextOptionsExtension : IDbContextOptionsExtension
{
    public IStartupEventChannel StartupEventChannel { get; }

    public NOFDbContextOptionsExtension(IStartupEventChannel startupEventChannel)
    {
        StartupEventChannel = startupEventChannel ?? throw new ArgumentNullException(nameof(startupEventChannel));
    }

    public void ApplyServices(IServiceCollection services) { }

    public void Validate(IDbContextOptions options) { }

    public DbContextOptionsExtensionInfo Info => new NoFDbContextOptionsExtensionInfo(this);

    private sealed class NoFDbContextOptionsExtensionInfo : DbContextOptionsExtensionInfo
    {
        private readonly NOFDbContextOptionsExtension _extension;

        public NoFDbContextOptionsExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension)
        {
            _extension = (NOFDbContextOptionsExtension)extension;
        }

        public override bool IsDatabaseProvider => false;
        public override string LogFragment => "";

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
        {
            return other is NoFDbContextOptionsExtensionInfo otherTyped &&
                   ReferenceEquals(_extension.StartupEventChannel, otherTyped._extension.StartupEventChannel);
        }

        public override int GetServiceProviderHashCode()
        {
            return RuntimeHelpers.GetHashCode(_extension.StartupEventChannel);
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }
    }
}


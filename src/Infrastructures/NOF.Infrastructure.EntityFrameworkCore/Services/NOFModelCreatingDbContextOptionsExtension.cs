using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class NOFModelCreatingDbContextOptionsExtension : IDbContextOptionsExtension
{
    public IReadOnlyList<IDbContextModelCreatingContributor> Contributors { get; init; } = [];

    public void ApplyServices(IServiceCollection services)
    {
    }

    public void Validate(IDbContextOptions options)
    {
    }

    public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

    public void ApplyModelCreating(ModelBuilder modelBuilder)
    {
        var adapter = new EfCoreModelBuilderAdapter(modelBuilder);
        foreach (var contributor in Contributors)
        {
            contributor.Configure(adapter);
        }
    }

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        public ExtensionInfo(IDbContextOptionsExtension extension) : base(extension)
        {
        }

        private new NOFModelCreatingDbContextOptionsExtension Extension
            => (NOFModelCreatingDbContextOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider => false;

        public override string LogFragment
            => $"NOFModelCreating(Contributors={Extension.Contributors.Count}) ";

        public override int GetServiceProviderHashCode()
        {
            var hashCode = new HashCode();

            foreach (var contributor in Extension.Contributors)
            {
                hashCode.Add(contributor);
            }

            return hashCode.ToHashCode();
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
        {
            if (other is not ExtensionInfo otherInfo)
            {
                return false;
            }

            return GetServiceProviderHashCode() == otherInfo.GetServiceProviderHashCode();
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            debugInfo["NOF:ModelCreatingContributors"] = Extension.Contributors.Count.ToString();
            debugInfo["NOF:ModelCreatingHash"] = GetServiceProviderHashCode().ToString();
        }
    }
}

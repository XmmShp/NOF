using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace NOF;

public record DbContextModelCreating(ModelBuilder Builder);

[Table(nameof(StateMachineContextInfo))]
internal sealed class StateMachineContextInfo
{
    public required string CorrelationId { get; set; }
    public required string DefinitionType { get; set; }
    public required string ContextType { get; set; }
    public required string ContextData { get; set; }
    public required int State { get; set; }
}

public abstract class NOFDbContext : DbContext
{
    private readonly IStartupEventChannel _startupEventChannel;
    protected NOFDbContext(DbContextOptions options) : base(options)
    {
        var extension = options.FindExtension<NOFDbContextOptionsExtension>();
        _startupEventChannel = extension?.StartupEventChannel ?? throw new InvalidOperationException("EventDispatcher is not configured in NOFDbContextOptionsExtension.");
    }

    internal DbSet<StateMachineContextInfo> StateMachineContexts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StateMachineContextInfo>(entity =>
        {
            entity.HasKey(e => new { e.CorrelationId, e.DefinitionType });
            entity.Property(e => e.ContextType).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.ContextData).IsRequired();
        });
        base.OnModelCreating(modelBuilder);
        _startupEventChannel.Publish(new DbContextModelCreating(modelBuilder));
    }
}

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
using Microsoft.EntityFrameworkCore;
using NOF.Application;

namespace NOF.Infrastructure.EntityFrameworkCore;

public abstract class NOFDbContext : DbContext
{
	private readonly DbContextOptions _options;

	protected NOFDbContext(DbContextOptions options) : base(options)
	{
		_options = options;
	}

	internal DbSet<NOFStateMachineContext> NOFStateMachineContexts { get; set; }
	internal DbSet<NOFInboxMessage> NOFInboxMessages { get; set; }
	internal DbSet<NOFOutboxMessage> NOFOutboxMessages { get; set; }
	internal DbSet<NOFTenant> NOFTenants { get; set; }

	/// <summary>
	/// Override this method to specify additional entity types that should be ignored
	/// in tenant mode, beyond those marked with <see cref="HostOnlyAttribute"/>.
	/// </summary>
	protected virtual Type[] GetTenantIgnoredEntityTypes() => [typeof(NOFTenant)];

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

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		base.ConfigureConventions(configurationBuilder);

		// In tenant mode, register a finalizing convention that removes [HostOnly] entities
		// after all OnModelCreating configurations have been applied
		var tenantExtension = _options.FindExtension<NOFTenantDbContextOptionsExtension>();
		if (tenantExtension is not null && !string.IsNullOrWhiteSpace(tenantExtension.TenantId))
		{
			var additionalIgnoredTypes = GetTenantIgnoredEntityTypes();
			tenantExtension.TenantIgnoredEntityTypes = additionalIgnoredTypes;
			configurationBuilder.Conventions.Add(_ => new HostOnlyModelFinalizingConvention(additionalIgnoredTypes));
		}
	}
}

using NOF.Application;

namespace NOF.Infrastructure;

public sealed class NOFTenantModelCreatingContributor : IDbContextModelCreatingContributor
{
    public void Configure(IDbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NOFTenant>(entity =>
        {
            entity.IsHostOnly();
            entity.ToTable(nameof(NOFTenant));
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
        });
    }
}

public sealed class NOFInboxMessageModelCreatingContributor : IDbContextModelCreatingContributor
{
    public void Configure(IDbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NOFInboxMessage>(entity =>
        {
            entity.IsHostOnly();
            entity.ToTable(nameof(NOFInboxMessage));
            entity.HasKey(nameof(NOFInboxMessage.Id), nameof(NOFInboxMessage.HandlerType));
            entity.HasIndex(nameof(NOFInboxMessage.Status), nameof(NOFInboxMessage.CreatedAtUtc));
            entity.HasIndex(nameof(NOFInboxMessage.Status), nameof(NOFInboxMessage.ClaimExpiresAtUtc));
            entity.HasIndex(e => e.ClaimedBy);
            entity.Property(e => e.PayloadType).HasMaxLength(512).IsRequired();
            entity.Property(e => e.HandlerType).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Headers).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2048);
            entity.Property(e => e.ClaimedBy).HasMaxLength(256);
        });
    }
}

public sealed class NOFOutboxMessageModelCreatingContributor : IDbContextModelCreatingContributor
{
    public void Configure(IDbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NOFOutboxMessage>(entity =>
        {
            entity.IsHostOnly();
            entity.ToTable(nameof(NOFOutboxMessage));
            entity.HasKey(e => e.Id);
            entity.HasIndex(nameof(NOFOutboxMessage.Status), nameof(NOFOutboxMessage.CreatedAtUtc));
            entity.HasIndex(nameof(NOFOutboxMessage.Status), nameof(NOFOutboxMessage.ClaimExpiresAtUtc));
            entity.HasIndex(e => e.ClaimedBy);
            entity.Property(e => e.PayloadType).HasMaxLength(512).IsRequired();
            entity.Property(e => e.DispatchTypes).IsRequired();
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Headers).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2048);
            entity.Property(e => e.ClaimedBy).HasMaxLength(256);
            entity.Property(e => e.TraceParent).HasMaxLength(128);
            entity.HasIndex(e => e.TraceParent);
        });
    }
}

public sealed class NOFStateMachineContextModelCreatingContributor : IDbContextModelCreatingContributor
{
    public void Configure(IDbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NOFStateMachineContext>(entity =>
        {
            entity.IsHostOnly();
            entity.ToTable(nameof(NOFStateMachineContext));
            entity.HasKey(
                nameof(NOFStateMachineContext.CorrelationId),
                nameof(NOFStateMachineContext.DefinitionTypeName));
            entity.Property(e => e.CorrelationId).IsRequired();
            entity.Property(e => e.DefinitionTypeName).IsRequired();
        });
    }
}

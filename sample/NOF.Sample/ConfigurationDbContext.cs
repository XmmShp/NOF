using Microsoft.EntityFrameworkCore;
using NOF.Sample.Application.Entities;

namespace NOF.Sample;

public class ConfigurationDbContext : NOFDbContext
{
    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) : base(options)
    {
    }

    public DbSet<ConfigNode> ConfigNodes { get; set; }
    public DbSet<ConfigNodeChildren> ConfigNodeChildren { get; set; }

    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.RegisterAllInEfCoreConverters();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConfigNode>(entity =>
        {
            entity.ToTable("ConfigNode");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.ActiveFileName).HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();

            entity.OwnsMany<ConfigFile>("_configFiles", file =>
            {
                file.ToTable("ConfigFile");
                file.WithOwner().HasForeignKey("NodeId");
                file.HasKey("Id");
                file.Property("Id").ValueGeneratedOnAdd();

                file.Property(f => f.Name).HasMaxLength(100);
                // Content can be large
            });

            entity.Ignore(e => e.ConfigFiles);
        });

        // 配置读模型：ConfigNodeChildren
        modelBuilder.Entity<ConfigNodeChildren>(entity =>
        {
            entity.ToTable("ConfigNodeChildren");
            entity.HasKey(e => e.NodeId);

            // PostgreSQL 数组类型
            entity.Property(e => e.ChildrenIds)
                .HasColumnType("bigint[]")
                .IsRequired();
        });
    }
}

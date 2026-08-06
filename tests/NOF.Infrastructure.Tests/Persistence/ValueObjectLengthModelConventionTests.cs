using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NOF.Domain;
using NOF.Infrastructure.EntityFrameworkCore;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

public sealed class ValueObjectLengthModelConventionTests
{
    [Fact]
    public void ModelConvention_AppliesDeclaredLength()
    {
        using var context = new AutomaticLengthDbContext(CreateOptions<AutomaticLengthDbContext>());

        var entityType = context.Model.FindEntityType(typeof(LengthEntity));
        var property = entityType!.FindProperty(nameof(LengthEntity.Name));

        Assert.Equal(42, property!.GetMaxLength());
    }

    [Fact]
    public void ModelConvention_AllowsMatchingExplicitLength()
    {
        using var context = new MatchingLengthDbContext(CreateOptions<MatchingLengthDbContext>());

        var entityType = context.Model.FindEntityType(typeof(LengthEntity));
        var property = entityType!.FindProperty(nameof(LengthEntity.Name));

        Assert.Equal(42, property!.GetMaxLength());
    }

    [Fact]
    public void ModelConvention_RejectsDynamicConflictingLength()
    {
        using var context = new ConflictingLengthDbContext(CreateOptions<ConflictingLengthDbContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => _ = context.Model);

        Assert.Contains(nameof(LengthEntity.Name), exception.Message);
        Assert.Contains("maximum length 41", exception.Message);
        Assert.Contains("maximum length 42", exception.Message);
    }

    private static DbContextOptions<TContext> CreateOptions<TContext>()
        where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>()
            .UseSqlite("Data Source=:memory:");
        optionsBuilder.ReplaceService<IModelCustomizer, NOFModelCustomizer>();
        optionsBuilder.ReplaceService<IValueConverterSelector, ValueObjectValueConverterSelector>();
        return optionsBuilder.Options;
    }

    [ValueObjectLength(42)]
    private readonly struct LimitedName : IValueObject<string>
    {
        private readonly string _value;

        private LimitedName(string value)
        {
            _value = value;
        }

        public static LimitedName Of(string value) => new(value);

        public static explicit operator string(LimitedName value) => value._value;
    }

    private sealed class LengthEntity
    {
        public int Id { get; set; }

        public LimitedName Name { get; set; }
    }

    private abstract class LengthDbContext(DbContextOptions options) : NOFDbContext(options)
    {
        protected void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LengthEntity>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Name);
            });
        }
    }

    private sealed class AutomaticLengthDbContext(DbContextOptions<AutomaticLengthDbContext> options)
        : LengthDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureEntity(modelBuilder);
        }
    }

    private sealed class MatchingLengthDbContext(DbContextOptions<MatchingLengthDbContext> options)
        : LengthDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureEntity(modelBuilder);
            modelBuilder.Entity<LengthEntity>().Property(item => item.Name).HasMaxLength(42);
        }
    }

    private sealed class ConflictingLengthDbContext(DbContextOptions<ConflictingLengthDbContext> options)
        : LengthDbContext(options)
    {
        private static readonly int ConfiguredLength = 41;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureEntity(modelBuilder);
            modelBuilder.Entity<LengthEntity>().Property(item => item.Name).HasMaxLength(ConfiguredLength);
        }
    }
}

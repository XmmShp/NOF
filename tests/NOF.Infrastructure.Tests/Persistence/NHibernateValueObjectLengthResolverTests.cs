using NOF.Domain;
using NOF.Infrastructure.NHibernate;
using System.Reflection;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

public sealed class NHibernateValueObjectLengthResolverTests
{
    private static readonly PropertyInfo NameProperty = typeof(LengthEntity)
        .GetProperty(nameof(LengthEntity.Name))!;

    [Fact]
    public void Resolve_UsesValueObjectLengthWhenNoLengthWasConfigured()
    {
        var maximumLength = NHibernateValueObjectLengthResolver.Resolve(
            typeof(LengthEntity),
            NameProperty,
            configuredLength: null);

        Assert.Equal(42, maximumLength);
    }

    [Fact]
    public void Resolve_AllowsMatchingExplicitLength()
    {
        var maximumLength = NHibernateValueObjectLengthResolver.Resolve(
            typeof(LengthEntity),
            NameProperty,
            configuredLength: 42);

        Assert.Equal(42, maximumLength);
    }

    [Fact]
    public void Resolve_RejectsDynamicConflictingLength()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            NHibernateValueObjectLengthResolver.Resolve(
                typeof(LengthEntity),
                NameProperty,
                configuredLength: 41));

        Assert.Contains(nameof(LengthEntity.Name), exception.Message);
        Assert.Contains("maximum length 41", exception.Message);
        Assert.Contains("maximum length 42", exception.Message);
    }

    [ValueObjectLength(42)]
    private readonly partial struct LimitedName : IValueObject<string>
    {
    }

    private sealed class LengthEntity
    {
        public LimitedName Name { get; set; }
    }
}

using System.Linq.Expressions;
using Xunit;

namespace NOF.Application.Tests;

public class MapperTests
{
    [Fact]
    public void Current_WithoutAmbientScope_ThrowsDescriptiveException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Mapper.Current);

        Assert.Contains("No ambient IMapper", exception.Message);
    }

    [Fact]
    public void PushCurrent_RestoresNestedAmbientScopes()
    {
        var outer = CreateMapper(value => "outer:" + value);
        var inner = CreateMapper(value => "inner:" + value);

        using (Mapper.PushCurrent(outer))
        {
            Assert.Same(outer, Mapper.Current);

            using (Mapper.PushCurrent(inner))
            {
                Assert.Same(inner, Mapper.Current);
            }

            Assert.Same(outer, Mapper.Current);
        }

        Assert.Throws<InvalidOperationException>(() => Mapper.Current);
    }

    [Fact]
    public async Task PushCurrent_IsIsolatedBetweenParallelAsyncFlows()
    {
        var first = CreateMapper(value => "first:" + value);
        var second = CreateMapper(value => "second:" + value);

        static async Task AssertAmbientAsync(IMapper expected)
        {
            using var _ = Mapper.PushCurrent(expected);
            await Task.Yield();
            Assert.Same(expected, Mapper.Current);
        }

        await Task.WhenAll(
            Task.Run(() => AssertAmbientAsync(first)),
            Task.Run(() => AssertAmbientAsync(second)));
    }

    [Fact]
    public void ProjectTo_UsesAmbientMapperAndOnlyRequiresDestinationType()
    {
        var mapper = CreateMapper(value => "value:" + value);

        using var _ = Mapper.PushCurrent(mapper);
        var result = new[] { 7 }.AsQueryable().ProjectTo<string>().Single();

        Assert.Equal("value:7", result);
    }

    [Fact]
    public void ProjectTo_ExplicitOverload_DoesNotRequireAmbientMapper()
    {
        var mapper = CreateMapper(value => "value:" + value);

        var result = new[] { 7 }.AsQueryable().ProjectTo<string>(mapper).Single();

        Assert.Equal("value:7", result);
    }

    [Fact]
    public void ProjectTo_ExtensionReceivesNonGenericQueryable()
    {
        var overloads = typeof(MappingQueryableExtensions)
            .GetMethods()
            .Where(method => method.Name == nameof(MappingQueryableExtensions.ProjectTo))
            .ToArray();

        Assert.NotEmpty(overloads);
        Assert.All(overloads, method => Assert.Equal(typeof(IQueryable), method.GetParameters()[0].ParameterType));
    }

    private static ExpressionMapper CreateMapper(Expression<Func<int, string>> expression)
    {
        var registry = new MappingRegistry();
        registry.Add(MappingRegistration.Of(expression));
        return new ExpressionMapper(registry);
    }
}

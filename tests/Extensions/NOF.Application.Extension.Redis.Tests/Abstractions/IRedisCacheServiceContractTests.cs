using FluentAssertions;
using NOF.Application;
using System.Reflection;
using Xunit;

namespace NOF.Application.Extension.Redis.Tests.Abstractions;

public sealed class IRedisCacheServiceContractTests
{
    [Fact]
    public void IRedisCacheService_ShouldInherit_ICacheService()
    {
        typeof(ICacheService).IsAssignableFrom(typeof(IRedisCacheService)).Should().BeTrue();
    }

    [Fact]
    public void IRedisCacheService_ShouldExposeCacheKeyBasedConvenienceOverloads()
    {
        var methods = typeof(IRedisCacheService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(HasCacheKeyFirstParameter)
            .ToArray();

        methods.Length.Should().BeGreaterThanOrEqualTo(20);
        methods.Select(m => m.Name).Should().Contain([
            "HashSetAsync",
            "HashGetAsync",
            "SetAddAsync",
            "ListRangeAsync",
            "SortedSetIncrementScoreAsync"
        ]);
    }

    private static bool HasCacheKeyFirstParameter(MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return false;
        }

        var first = parameters[0].ParameterType;
        return first.IsGenericType && first.GetGenericTypeDefinition() == typeof(CacheKey<>);
    }
}

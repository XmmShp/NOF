using System.Reflection;
using Xunit;

namespace NOF.Application.Extension.Redis.Tests.Abstractions;

public sealed class IRedisCacheServiceContractTests
{
    [Fact]
    public void IRedisCacheService_ShouldInherit_ICacheService()
    {
        Assert.True(
        typeof(ICacheService).IsAssignableFrom(typeof(IRedisCacheService)));
    }

    [Fact]
    public void IRedisCacheService_ShouldExposeCacheKeyBasedConvenienceOverloads()
    {
        var methods = typeof(NOFApplicationExtensionRedisExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(HasCacheKeyFirstParameter)
            .ToArray();

        Assert.True(methods.Length >= 20);
        var names = methods.Select(m => m.Name).ToArray();
        Assert.Contains("HashSetAsync", names);
        Assert.Contains("HashGetAsync", names);
        Assert.Contains("SetAddAsync", names);
        Assert.Contains("ListRangeAsync", names);
        Assert.Contains("SortedSetIncrementScoreAsync", names);
    }

    private static bool HasCacheKeyFirstParameter(MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length < 2)
        {
            return false;
        }

        var first = parameters[1].ParameterType;
        return first.IsGenericType && first.GetGenericTypeDefinition() == typeof(CacheKey<>);
    }
}


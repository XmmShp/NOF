using FluentAssertions;
using Xunit;
using BusExtensions = NOF.__NOF_Infrastructure_MassTransit_Extensions__;

namespace NOF.Infrastructure.Tests.Extensions;

public class BusExtensionsTests
{
    private class TestCommand : ICommand
    {
        public string Name { get; set; } = string.Empty;
    }

    private class TestCommandWithResponse : ICommand<string>
    {
        public string Input { get; set; } = string.Empty;
    }

    [Fact]
    public void CreateNoValueExecutor_ShouldCreateValidExecutor()
    {
        var commandType = typeof(TestCommand);
        var executor = BusExtensions.CreateExecutor<Result>(commandType);
        executor.Should().NotBeNull();
    }

    [Fact]
    public void CreateValueExecutor_ShouldCreateValidExecutor()
    {
        var commandType = typeof(TestCommandWithResponse);
        var executor = BusExtensions.CreateExecutor<Result<string>>(commandType);
        executor.Should().NotBeNull();
    }

    [Fact]
    public void ValueCommandCache_ShouldBeAccessible()
    {
        var cache = BusExtensions.ValueCommandCache<Result<string>>.Cache;
        cache.Should().NotBeNull();
    }

    [Fact]
    public void BusExtensions_ShouldBeStaticClass()
    {
        var type = typeof(BusExtensions);
        type.IsClass.Should().BeTrue();
        type.IsAbstract.Should().BeTrue();
        type.IsSealed.Should().BeTrue();
    }
}

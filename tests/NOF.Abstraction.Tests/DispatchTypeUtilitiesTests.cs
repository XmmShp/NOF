using Xunit;

namespace NOF.Abstraction.Tests;

public sealed class DispatchTypeUtilitiesTests
{
    [Fact]
    public void GetSelfAndBaseTypesAndInterfaces_ShouldIncludeSelfBaseTypesAndInterfaces()
    {
        var types = DispatchTypeUtilities.GetSelfAndBaseTypesAndInterfaces(typeof(DerivedMessage));

        Assert.Equal(typeof(DerivedMessage), types[0]);
        Assert.Contains(typeof(BaseMessage), types);
        Assert.Contains(typeof(object), types);
        Assert.Contains(typeof(IFirstContract), types);
        Assert.Contains(typeof(ISecondContract), types);
    }

    private interface IFirstContract;

    private interface ISecondContract;

    private abstract class BaseMessage : IFirstContract;

    private sealed class DerivedMessage : BaseMessage, ISecondContract;
}

using System.ComponentModel.DataAnnotations;
using Xunit;

namespace NOF.Abstraction.Tests;

public sealed class EnumExtensionsTests
{
    [Fact]
    public void ToDisplayString_WithDisplayAttribute_ShouldReturnDisplayName()
    {
        var result = TestStatus.InProgress.ToDisplayString();

        Assert.Equal("In progress", result);
    }

    [Fact]
    public void ToDisplayString_WithoutDisplayAttribute_ShouldReturnEnumName()
    {
        var result = TestStatus.Completed.ToDisplayString();

        Assert.Equal(nameof(TestStatus.Completed), result);
    }

    [Fact]
    public void ToDisplayString_WithUndefinedValue_ShouldReturnNumericValue()
    {
        var result = ((TestStatus)42).ToDisplayString();

        Assert.Equal("42", result);
    }

    [Fact]
    public void ToDisplayString_CalledRepeatedly_ShouldReturnSameDisplayName()
    {
        var results = Enumerable.Range(0, 100)
            .Select(static _ => TestStatus.InProgress.ToDisplayString());

        Assert.All(results, static result => Assert.Equal("In progress", result));
    }

    [Fact]
    public void ToDisplayString_WithCombinedFlags_ShouldReturnCombinedDisplayNames()
    {
        var result = (TestPermissions.Read | TestPermissions.Execute).ToDisplayString();

        Assert.Equal("Can read, Can execute", result);
    }

    [Fact]
    public void ToDisplayString_WithNamedCombinedFlag_ShouldReturnItsDisplayName()
    {
        var result = TestPermissions.ReadWrite.ToDisplayString();

        Assert.Equal("Read and write", result);
    }

    [Fact]
    public void ToDisplayString_WithUnknownFlag_ShouldReturnNumericValue()
    {
        var result = ((TestPermissions)8).ToDisplayString();

        Assert.Equal("8", result);
    }

    private enum TestStatus
    {
        [Display(Name = "In progress")]
        InProgress,

        Completed
    }

    [Flags]
    private enum TestPermissions
    {
        None = 0,

        [Display(Name = "Can read")]
        Read = 1,

        [Display(Name = "Can write")]
        Write = 2,

        [Display(Name = "Read and write")]
        ReadWrite = Read | Write,

        [Display(Name = "Can execute")]
        Execute = 4
    }
}

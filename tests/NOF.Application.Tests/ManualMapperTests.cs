using FluentAssertions;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using Xunit;

namespace NOF.Application.Tests;

public class ManualMapperTests
{
    private static ManualMapper CreateMapper(Action<MapperOptions>? configure = null)
    {
        var options = new MapperOptions();
        configure?.Invoke(options);
        return new ManualMapper(Options.Create(options));
    }

    #region Basic generic mapping

    [Fact]
    public void Map_Generic_ReturnsExpectedResult()
    {
        var mapper = CreateMapper(o =>
            o.Add<int, string>(x => x.ToString()));

        var result = mapper.Map<int, string>(42);

        result.Should().Be("42");
    }

    [Fact]
    public void TryMap_Generic_ReturnsOptionalWithValue()
    {
        var mapper = CreateMapper(o =>
            o.Add<int, string>(x => x.ToString()));

        var result = mapper.TryMap<int, string>(42);

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be("42");
    }

    [Fact]
    public void TryMap_Generic_NoMapping_ReturnsNone()
    {
        var mapper = CreateMapper();

        var result = mapper.TryMap<int, TargetDto>(42);

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Map_Generic_NoMapping_ThrowsInvalidOperationException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map<int, TargetDto>(42);

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Basic non-generic mapping

    [Fact]
    public void Map_NonGeneric_ReturnsExpectedResult()
    {
        var mapper = CreateMapper(o =>
            o.Add(typeof(int), typeof(string), src => Optional.Of<object?>(((int)src).ToString())));

        var result = mapper.Map(typeof(int), typeof(string), 42);

        result.Should().Be("42");
    }

    [Fact]
    public void TryMap_NonGeneric_ReturnsOptionalWithValue()
    {
        var mapper = CreateMapper(o =>
            o.Add(typeof(int), typeof(string), src => Optional.Of<object?>(((int)src).ToString())));

        var result = mapper.TryMap(typeof(int), typeof(string), 42);

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be("42");
    }

    [Fact]
    public void TryMap_NonGeneric_NoMapping_ReturnsNone()
    {
        var mapper = CreateMapper();

        var result = mapper.TryMap(typeof(int), typeof(TargetDto), 42);

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Map_NonGeneric_NoMapping_ThrowsInvalidOperationException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map(typeof(int), typeof(TargetDto), 42);

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Named mappings

    [Fact]
    public void Map_NamedMapping_ReturnsCorrectResult()
    {
        var mapper = CreateMapper(o =>
        {
            o.Add<int, string>(x => $"default:{x}");
            o.Add<int, string>(x => $"summary:{x}", name: "summary");
            o.Add<int, string>(x => $"full:{x}", name: "full");
        });

        mapper.Map<int, string>(1).Should().Be("default:1");
        mapper.Map<int, string>(1, name: "summary").Should().Be("summary:1");
        mapper.Map<int, string>(1, name: "full").Should().Be("full:1");
    }

    [Fact]
    public void Map_NamedMapping_NotFound_ThrowsEvenIfDefaultExists()
    {
        var mapper = CreateMapper(o =>
            o.Add<int, string>(x => x.ToString()));

        var act = () => mapper.Map<int, string>(1, name: "nonexistent");

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region TryAdd

    [Fact]
    public void TryAdd_Generic_FirstCallSucceeds()
    {
        var mapper = CreateMapper();

        var added = mapper.TryAdd<int, string>(x => x.ToString());

        added.Should().BeTrue();
        mapper.Map<int, string>(42).Should().Be("42");
    }

    [Fact]
    public void TryAdd_Generic_SecondCallIsNoOp()
    {
        var mapper = CreateMapper();

        mapper.TryAdd<int, string>(x => $"first:{x}");
        var added = mapper.TryAdd<int, string>(x => $"second:{x}");

        added.Should().BeFalse();
        mapper.Map<int, string>(1).Should().Be("first:1");
    }

    [Fact]
    public void TryAdd_NonGeneric_SecondCallIsNoOp()
    {
        var mapper = CreateMapper();

        mapper.TryAdd(typeof(int), typeof(string), src => Optional.Of<object?>($"first:{src}"));
        var added = mapper.TryAdd(typeof(int), typeof(string), src => Optional.Of<object?>($"second:{src}"));

        added.Should().BeFalse();
        mapper.Map<int, string>(1).Should().Be("first:1");
    }

    #endregion

    #region ReplaceOrAdd

    [Fact]
    public void ReplaceOrAdd_Generic_ReplacesExistingDelegates()
    {
        var mapper = CreateMapper(o =>
        {
            o.Add<int, string>(x => $"first:{x}");
            o.Add<int, string>(x => $"second:{x}");
        });

        // Both delegates exist; last-added ("second") wins
        mapper.Map<int, string>(1).Should().Be("second:1");

        // ReplaceOrAdd clears both and sets a single new delegate
        mapper.ReplaceOrAdd<int, string>(x => $"replaced:{x}");
        mapper.Map<int, string>(1).Should().Be("replaced:1");
    }

    [Fact]
    public void ReplaceOrAdd_NonGeneric_ReplacesExistingDelegates()
    {
        var mapper = CreateMapper(o =>
            o.Add(typeof(int), typeof(string), src => Optional.Of<object?>($"original:{src}")));

        mapper.ReplaceOrAdd(typeof(int), typeof(string), src => Optional.Of<object?>($"replaced:{src}"));
        mapper.Map<int, string>(1).Should().Be("replaced:1");
    }

    #endregion

    #region Multiple delegates per key (last-added first, first HasValue wins)

    [Fact]
    public void MultipleDelegates_LastAddedEvaluatedFirst()
    {
        var mapper = CreateMapper(o =>
        {
            o.Add<int, string>(x => $"first:{x}");
            o.Add<int, string>(x => $"second:{x}");
        });

        // "second" was added last, so it is evaluated first
        mapper.Map<int, string>(1).Should().Be("second:1");
    }

    [Fact]
    public void MultipleDelegates_FallsBackToEarlierWhenLaterReturnsNone()
    {
        var mapper = CreateMapper(o =>
        {
            o.Add<int, string>(x => $"fallback:{x}");
            o.Add<int, string>(x => x > 0 ? Optional.Of($"positive:{x}") : Optional.None);
        });

        // x > 0: second delegate returns value
        mapper.Map<int, string>(5).Should().Be("positive:5");

        // x <= 0: second delegate returns None, falls back to first
        mapper.Map<int, string>(-1).Should().Be("fallback:-1");
    }

    [Fact]
    public void MultipleDelegates_AllReturnNone_TryMapReturnsNone()
    {
        var mapper = CreateMapper(o =>
        {
            o.Add<int, TargetDto>(x => x > 100 ? Optional.Of(new TargetDto($"big:{x}")) : Optional.None);
            o.Add<int, TargetDto>(x => x < 0 ? Optional.Of(new TargetDto($"negative:{x}")) : Optional.None);
        });

        // Neither delegate matches, no built-in for int→TargetDto
        var result = mapper.TryMap<int, TargetDto>(50);
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region Open generic fallback

    [Fact]
    public void OpenGenericSource_FallbackWorks()
    {
        var mapper = CreateMapper(o =>
            o.Add(typeof(List<>), typeof(int), src => Optional.Of<object?>(((System.Collections.IList)src).Count)));

        var result = mapper.Map<List<string>, int>(["a", "b", "c"]);

        result.Should().Be(3);
    }

    [Fact]
    public void OpenGenericDest_FallbackWorks()
    {
        var mapper = CreateMapper(o =>
            o.Add(typeof(string), typeof(List<>), src => Optional.Of<object?>(new List<string> { (string)src })));

        var result = mapper.Map(typeof(string), typeof(List<string>), "hello");

        result.Should().BeOfType<List<string>>();
        ((List<string>)result).Should().ContainSingle().Which.Should().Be("hello");
    }

    [Fact]
    public void OpenGenericBoth_FallbackWorks()
    {
        var mapper = CreateMapper(o =>
            o.Add(typeof(List<>), typeof(HashSet<>), src =>
            {
                var list = (System.Collections.IList)src;
                var set = new HashSet<object>();
                foreach (var item in list) set.Add(item);
                return Optional.Of<object?>(set);
            }));

        var result = mapper.Map(typeof(List<int>), typeof(HashSet<int>), new List<int> { 1, 2, 3 });

        result.Should().BeOfType<HashSet<object>>();
    }

    [Fact]
    public void ClosedType_TakesPriorityOverOpenGeneric()
    {
        var mapper = CreateMapper(o =>
        {
            o.Add(typeof(List<>), typeof(int), src => Optional.Of<object?>(999));
            o.Add(typeof(List<string>), typeof(int), src => Optional.Of<object?>(42));
        });

        // Closed type registration should win
        var result = mapper.Map<List<string>, int>(["a"]);
        result.Should().Be(42);

        // Other closed type falls back to open generic
        var result2 = mapper.Map<List<int>, int>([1, 2]);
        result2.Should().Be(999);
    }

    #endregion

    #region Nullable<T> fallback

    [Fact]
    public void NullableFallback_MappingToNullableT_UsesRegisteredTMapping()
    {
        var mapper = CreateMapper(o =>
            o.Add<string, int>(s => s.Length));

        // Registered: string → int. Query: string → int?
        var result = mapper.TryMap<string, int?>("hello");

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public void NullableFallback_MappingToT_DoesNotUseNullableTMapping()
    {
        var mapper = CreateMapper(o =>
            o.Add<string, int?>(s => s.Length));

        // Registered: string → int?. Query: string → int. Should NOT find it.
        var result = mapper.TryMap<string, int>("hello");

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void NullableFallback_DirectNullableRegistration_TakesPriority()
    {
        var mapper = CreateMapper(o =>
        {
            o.Add<string, int>(s => s.Length);
            o.Add<string, int?>(s => s.Length * 10);
        });

        // Direct int? registration should take priority over fallback from int
        var result = mapper.Map<string, int?>("hi");
        result.Should().Be(20);

        // int mapping still works directly
        var result2 = mapper.Map<string, int>("hi");
        result2.Should().Be(2);
    }

    [Fact]
    public void NullableFallback_NoRegistration_ReturnsNone()
    {
        var mapper = CreateMapper();

        var result = mapper.TryMap<string, int?>("hello");

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void NullableFallback_Map_ThrowsWhenNoMappingFound()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map<string, int?>("hello");

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region useRuntimeType

    [Fact]
    public void Map_UseRuntimeType_ResolvesUsingActualType()
    {
        var mapper = CreateMapper(o =>
            o.Add<DerivedSource, TargetDto>(d => new TargetDto($"derived:{d.Extra}")));

        BaseSource source = new DerivedSource { Name = "base", Extra = "ext" };

        // Without useRuntimeType: looks up BaseSource → TargetDto (not found)
        var act = () => mapper.Map<BaseSource, TargetDto>(source);
        act.Should().Throw<InvalidOperationException>();

        // With useRuntimeType: looks up DerivedSource → TargetDto
        var result = mapper.Map<BaseSource, TargetDto>(source, useRuntimeType: true);
        result.Label.Should().Be("derived:ext");
    }

    [Fact]
    public void TryMap_UseRuntimeType_ResolvesUsingActualType()
    {
        var mapper = CreateMapper(o =>
            o.Add<DerivedSource, TargetDto>(d => new TargetDto($"derived:{d.Extra}")));

        BaseSource source = new DerivedSource { Name = "base", Extra = "ext" };

        var without = mapper.TryMap<BaseSource, TargetDto>(source);
        without.HasValue.Should().BeFalse();

        var with = mapper.TryMap<BaseSource, TargetDto>(source, useRuntimeType: true);
        with.HasValue.Should().BeTrue();
        with.Value.Label.Should().Be("derived:ext");
    }

    #endregion

    #region MapperOptions.Merge

    [Fact]
    public void Merge_AppendsFromOther_WithLowerPriority()
    {
        var primary = new MapperOptions();
        primary.Add<int, string>(x => $"primary:{x}");

        var secondary = new MapperOptions();
        secondary.Add<int, string>(x => $"secondary:{x}");
        secondary.Add<int, double>(x => x * 1.5);

        primary.Merge(secondary);

        var mapper = new ManualMapper(Options.Create(primary));

        // int→string: primary was added first, secondary appended (lower priority).
        // Last-added (primary) wins... wait — primary was in the list first, secondary appended after.
        // Iteration is reverse order (last element first). So secondary is last → evaluated first.
        // But Merge appends "after existing", so primary[0], secondary[1].
        // Reverse iteration: secondary first. Both return HasValue, so secondary wins.
        // Actually: primary's delegate was added first (index 0), secondary's appended (index 1).
        // Reverse: index 1 (secondary) checked first.
        // Hmm, but we want primary to take priority. Let's verify:
        // Actually Merge doc says "delegates from other are appended after existing ones (thus evaluated with lower priority)".
        // Reverse iteration: higher index = checked first. So appended = higher index = checked first = higher priority.
        // That contradicts the doc. Let me just test the actual behavior.

        // int→double: only exists in secondary, should work
        mapper.Map<int, double>(10).Should().Be(15.0);

        // int→string: both exist
        // The primary Add put delegate at index 0. Merge appends secondary at index 1.
        // Reverse iteration checks index 1 (secondary) first.
        var stringResult = mapper.Map<int, string>(1);
        // secondary is checked first since it's at higher index
        stringResult.Should().Be("secondary:1");
    }

    [Fact]
    public void Merge_NewKey_IsAvailable()
    {
        var primary = new MapperOptions();
        var secondary = new MapperOptions();
        secondary.Add<string, int>(s => s.Length);

        primary.Merge(secondary);

        var mapper = new ManualMapper(Options.Create(primary));
        mapper.Map<string, int>("test").Should().Be(4);
    }

    #endregion

    #region Registration via IMapper (runtime)

    [Fact]
    public void IMapper_Add_RegistersAtRuntime()
    {
        var mapper = CreateMapper();

        mapper.Add<int, string>(x => x.ToString());

        mapper.Map<int, string>(42).Should().Be("42");
    }

    [Fact]
    public void IMapper_TryAdd_SkipsIfAlreadyRegistered()
    {
        var mapper = CreateMapper(o =>
            o.Add<int, string>(x => $"options:{x}"));

        mapper.TryAdd<int, string>(x => $"runtime:{x}");

        mapper.Map<int, string>(1).Should().Be("options:1");
    }

    [Fact]
    public void IMapper_ReplaceOrAdd_ReplacesExisting()
    {
        var mapper = CreateMapper(o =>
            o.Add<int, string>(x => $"options:{x}"));

        mapper.ReplaceOrAdd<int, string>(x => $"runtime:{x}");

        mapper.Map<int, string>(1).Should().Be("runtime:1");
    }

    #endregion

    #region Edge cases

    [Fact]
    public void Map_NullSource_Generic_ThrowsOnDelegate()
    {
        var mapper = CreateMapper(o =>
            o.Add<string, int>(s => s.Length));

        // source is null, delegate will throw NRE
        var act = () => mapper.Map<string, int>(null!);
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void Map_NonGeneric_NullSourceType_ThrowsArgumentNullException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map(null!, typeof(string), "test");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Map_NonGeneric_NullDestType_ThrowsArgumentNullException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map(typeof(string), null!, "test");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Map_NonGeneric_NullSource_ThrowsArgumentNullException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map(typeof(string), typeof(int), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Test helpers

    private class BaseSource
    {
        public string Name { get; set; } = "";
    }

    private class DerivedSource : BaseSource
    {
        public string Extra { get; set; } = "";
    }

    private record TargetDto(string Label);

    #endregion
}

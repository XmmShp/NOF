using FluentAssertions;
using NOF.Application;
using System.Collections;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public class ManualMapperTests
{
    private static ManualMapper CreateMapper(Action<IMapper>? configure = null)
    {
        var mapper = new ManualMapper();
        configure?.Invoke(mapper);
        return mapper;
    }

    [Fact]
    public void Map_Generic_ReturnsExpectedResult()
    {
        var mapper = CreateMapper(m =>
            m.Add<int, string>(x => x.ToString()));

        var result = mapper.Map<int, string>(42);

        result.Should().Be("42");
    }

    [Fact]
    public void TryMap_Generic_ReturnsTrueAndValue()
    {
        var mapper = CreateMapper(m =>
            m.Add<int, string>(x => x.ToString()));

        var found = mapper.TryMap<int, string>(42, out var result);

        found.Should().BeTrue();
        result.Should().Be("42");
    }

    [Fact]
    public void TryMap_Generic_NoMapping_ReturnsFalse()
    {
        var mapper = CreateMapper();

        var found = mapper.TryMap<int, TargetDto>(42, out var result);

        found.Should().BeFalse();
    }

    [Fact]
    public void Map_Generic_NoMapping_ThrowsInvalidOperationException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map<int, TargetDto>(42);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Map_NonGeneric_ReturnsExpectedResult()
    {
        var mapper = CreateMapper(m =>
            m.Add(typeof(int), typeof(string), (src, _) => ((int)src).ToString()));

        var result = mapper.Map(typeof(int), typeof(string), 42);

        result.Should().Be("42");
    }

    [Fact]
    public void TryMap_NonGeneric_ReturnsTrueAndValue()
    {
        var mapper = CreateMapper(m =>
            m.Add(typeof(int), typeof(string), (src, _) => ((int)src).ToString()));

        var found = mapper.TryMap(typeof(int), typeof(string), 42, out var result);

        found.Should().BeTrue();
        result.Should().Be("42");
    }

    [Fact]
    public void TryMap_NonGeneric_NoMapping_ReturnsFalse()
    {
        var mapper = CreateMapper();

        var found = mapper.TryMap(typeof(int), typeof(TargetDto), 42, out var result);

        found.Should().BeFalse();
    }

    [Fact]
    public void Map_NonGeneric_NoMapping_ThrowsInvalidOperationException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map(typeof(int), typeof(TargetDto), 42);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Map_NamedMapping_ReturnsCorrectResult()
    {
        var mapper = CreateMapper(m =>
        {
            m.Add<int, string>(x => $"default:{x}");
            m.Add<int, string>(x => $"summary:{x}", name: "summary");
            m.Add<int, string>(x => $"full:{x}", name: "full");
        });

        mapper.Map<int, string>(1).Should().Be("default:1");
        mapper.Map<int, string>(1, name: "summary").Should().Be("summary:1");
        mapper.Map<int, string>(1, name: "full").Should().Be("full:1");
    }

    [Fact]
    public void Map_NamedMapping_NotFound_ThrowsEvenIfDefaultExists()
    {
        var mapper = CreateMapper(m =>
            m.Add<int, string>(x => x.ToString()));

        var act = () => mapper.Map<int, string>(1, name: "nonexistent");

        act.Should().Throw<InvalidOperationException>();
    }

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

        mapper.TryAdd(typeof(int), typeof(string), (src, _) => $"first:{src}");
        var added = mapper.TryAdd(typeof(int), typeof(string), (src, _) => $"second:{src}");

        added.Should().BeFalse();
        mapper.Map<int, string>(1).Should().Be("first:1");
    }

    [Fact]
    public void Add_Generic_ReplacesExistingDelegate()
    {
        var mapper = CreateMapper(m =>
        {
            m.Add<int, string>(x => $"first:{x}");
            m.Add<int, string>(x => $"second:{x}");
        });

        mapper.Map<int, string>(1).Should().Be("second:1");
    }

    [Fact]
    public void OpenGenericSource_FallbackWorks()
    {
        var mapper = CreateMapper(m =>
            m.Add(typeof(List<>), typeof(int), (src, _) => ((IList)src).Count));

        var result = mapper.Map<List<string>, int>(["a", "b", "c"]);

        result.Should().Be(3);
    }

    [Fact]
    public void OpenGenericDest_FallbackWorks()
    {
        var mapper = CreateMapper(m =>
            m.Add(typeof(string), typeof(List<>), (src, _) => new List<string> { (string)src }));

        var result = mapper.Map(typeof(string), typeof(List<string>), "hello");

        result.Should().BeOfType<List<string>>();
        ((List<string>)result).Should().ContainSingle().Which.Should().Be("hello");
    }

    [Fact]
    public void OpenGenericBoth_FallbackWorks()
    {
        var mapper = CreateMapper(o =>
            o.Add(typeof(List<>), typeof(HashSet<>), (src, _) =>
            {
                var list = (IList)src;
                var set = new HashSet<object>();
                foreach (var item in list)
                {
                    set.Add(item);
                }

                return set;
            }));

        var result = mapper.Map(typeof(List<int>), typeof(HashSet<int>), new List<int> { 1, 2, 3 });

        result.Should().BeOfType<HashSet<object>>();
    }

    [Fact]
    public void ClosedType_TakesPriorityOverOpenGeneric()
    {
        var mapper = CreateMapper(m =>
        {
            m.Add(typeof(List<>), typeof(int), (src, _) => 999);
            m.Add(typeof(List<string>), typeof(int), (src, _) => 42);
        });

        var result = mapper.Map<List<string>, int>(["a"]);
        result.Should().Be(42);

        var result2 = mapper.Map<List<int>, int>([1, 2]);
        result2.Should().Be(999);
    }

    [Fact]
    public void NullableFallback_MappingToNullableT_UsesRegisteredTMapping()
    {
        var mapper = CreateMapper(m =>
            m.Add<string, int>(s => s.Length));

        var found = mapper.TryMap<string, int?>("hello", out var result);

        found.Should().BeTrue();
        result.Should().Be(5);
    }

    [Fact]
    public void NullableFallback_MappingToT_DoesNotUseNullableTMapping()
    {
        var mapper = CreateMapper(m =>
            m.Add<string, int?>(s => s.Length));

        var act = () => mapper.Map<string, int>("hello");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NullableFallback_DirectNullableRegistration_TakesPriority()
    {
        var mapper = CreateMapper(m =>
        {
            m.Add<string, int>(s => s.Length);
            m.Add<string, int?>(s => s.Length * 10);
        });

        var result = mapper.Map<string, int?>("hi");
        result.Should().Be(20);

        var result2 = mapper.Map<string, int>("hi");
        result2.Should().Be(2);
    }

    [Fact]
    public void NullableFallback_NoRegistration_ThrowsInvalidOperationException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map<string, int?>("hello");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Map_UseRuntimeType_ResolvesUsingActualType()
    {
        var mapper = CreateMapper(m =>
            m.Add<DerivedSource, TargetDto>(d => new TargetDto($"derived:{d.Extra}")));

        BaseSource source = new DerivedSource { Name = "base", Extra = "ext" };

        var act = () => mapper.Map<BaseSource, TargetDto>(source);
        act.Should().Throw<InvalidOperationException>();

        var result = mapper.Map<BaseSource, TargetDto>(source, useRuntimeType: true);
        result.Label.Should().Be("derived:ext");
    }

    [Fact]
    public void TryMap_UseRuntimeType_ResolvesUsingActualType()
    {
        var mapper = CreateMapper(m =>
            m.Add<DerivedSource, TargetDto>(d => new TargetDto($"derived:{d.Extra}")));

        BaseSource source = new DerivedSource { Name = "base", Extra = "ext" };

        var withoutFound = mapper.TryMap<BaseSource, TargetDto>(source, out var without);
        withoutFound.Should().BeFalse();

        var withFound = mapper.TryMap<BaseSource, TargetDto>(source, out var with, useRuntimeType: true);
        withFound.Should().BeTrue();
        with.Label.Should().Be("derived:ext");
    }

    [Fact]
    public void Merge_ExistingKeysNotOverwritten()
    {
        var mapper = CreateMapper(m =>
        {
            m.Add<int, string>(x => $"primary:{x}");
            m.TryAdd<int, double>(x => x * 1.5);
        });

        mapper.Map<int, string>(1).Should().Be("primary:1");
        mapper.Map<int, double>(10).Should().Be(15.0);
    }

    [Fact]
    public void Merge_NewKey_IsAvailable()
    {
        var mapper = CreateMapper(m =>
        {
            m.TryAdd<string, int>(s => s.Length);
        });
        mapper.Map<string, int>("test").Should().Be(4);
    }

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
        var mapper = CreateMapper(m =>
            m.Add<int, string>(x => $"options:{x}"));

        mapper.TryAdd<int, string>(x => $"runtime:{x}");

        mapper.Map<int, string>(1).Should().Be("options:1");
    }

    [Fact]
    public void IMapper_Add_ReplacesExisting()
    {
        var mapper = CreateMapper(m =>
            m.Add<int, string>(x => $"options:{x}"));

        mapper.Add<int, string>(x => $"runtime:{x}");

        mapper.Map<int, string>(1).Should().Be("runtime:1");
    }

    [Fact]
    public void Add_WithMapper_DelegateCanUseMapperForNestedMapping()
    {
        var mapper = CreateMapper(m =>
        {
            m.Add<int, string>(x => $"mapped:{x}");
            m.Add<Wrapper<int>, string>((src, mm) => $"wrapped({mm.Map<int, string>(src.Inner)})");
        });

        var wrapped = new Wrapper<int>(5);
        mapper.Map<Wrapper<int>, string>(wrapped).Should().Be("wrapped(mapped:5)");
    }

    [Fact]
    public void TryAdd_WithMapper_DelegateCanUseMapper()
    {
        var mapper = CreateMapper(m =>
            m.Add<int, string>(x => x.ToString()));

        var added = mapper.TryAdd<Wrapper<int>, string>((src, m) => $"w:{m.Map<int, string>(src.Inner)}");
        added.Should().BeTrue();

        mapper.Map<Wrapper<int>, string>(new Wrapper<int>(7)).Should().Be("w:7");
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

    [Fact]
    public void NoBuiltInMappings_IntToString_Throws()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map<int, string>(42);
        act.Should().Throw<InvalidOperationException>();
    }

    private record Wrapper<T>(T Inner);

    private class BaseSource
    {
        public string Name { get; set; } = "";
    }

    private class DerivedSource : BaseSource
    {
        public string Extra { get; set; } = "";
    }

    private record TargetDto(string Label);
}

using NOF.Application;
using System.Collections;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public class ManualMapperTests
{
    private static ManualMapper CreateMapper(Action<MapperInfos>? configure = null)
    {
        var infos = new MapperInfos();
        configure?.Invoke(infos);
        return new ManualMapper(infos);
    }

    private static void Add<TSource, TDestination>(MapperInfos infos, Func<TSource, TDestination> mappingFunc, string? name = null)
        => infos.Add(MapperRegistration.Of(mappingFunc, name));

    private static void Add<TSource, TDestination>(MapperInfos infos, Func<TSource, IMapper, TDestination> mappingFunc, string? name = null)
        => infos.Add(MapperRegistration.Of(mappingFunc, name));

    private static void Add(MapperInfos infos, Type sourceType, Type destinationType, MapFunc mappingFunc, string? name = null)
        => infos.Add(new MapperRegistration(new MapKey(sourceType, destinationType, name), mappingFunc));

    [Fact]
    public void Map_Generic_ReturnsExpectedResult()
    {
        var mapper = CreateMapper(m =>
            Add(m, (int x) => x.ToString()));

        var result = mapper.Map<int, string>(42);
        Assert.Equal("42",

        result);
    }

    [Fact]
    public void TryMap_Generic_ReturnsTrueAndValue()
    {
        var mapper = CreateMapper(m =>
            Add(m, (int x) => x.ToString()));

        var found = mapper.TryMap<int, string>(42, out var result);
        Assert.True(

        found);
        Assert.Equal("42",
        result);
    }

    [Fact]
    public void TryMap_Generic_NoMapping_ReturnsFalse()
    {
        var mapper = CreateMapper();

        var found = mapper.TryMap<int, TargetDto>(42, out var result);
        Assert.False(

        found);
    }

    [Fact]
    public void Map_Generic_NoMapping_ThrowsInvalidOperationException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map<int, TargetDto>(42);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Map_NonGeneric_ReturnsExpectedResult()
    {
        var mapper = CreateMapper(m =>
            Add(m, typeof(int), typeof(string), (src, _) => ((int)src).ToString()));

        var result = mapper.Map(typeof(int), typeof(string), 42);
        Assert.Equal("42",

        result);
    }

    [Fact]
    public void TryMap_NonGeneric_ReturnsTrueAndValue()
    {
        var mapper = CreateMapper(m =>
            Add(m, typeof(int), typeof(string), (src, _) => ((int)src).ToString()));

        var found = mapper.TryMap(typeof(int), typeof(string), 42, out var result);
        Assert.True(

        found);
        Assert.Equal("42",
        result);
    }

    [Fact]
    public void TryMap_NonGeneric_NoMapping_ReturnsFalse()
    {
        var mapper = CreateMapper();

        var found = mapper.TryMap(typeof(int), typeof(TargetDto), 42, out var result);
        Assert.False(

        found);
    }

    [Fact]
    public void Map_NonGeneric_NoMapping_ThrowsInvalidOperationException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map(typeof(int), typeof(TargetDto), 42);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Map_NamedMapping_ReturnsCorrectResult()
    {
        var mapper = CreateMapper(m =>
        {
            Add(m, (int x) => $"default:{x}");
            Add(m, (int x) => $"summary:{x}", name: "summary");
            Add(m, (int x) => $"full:{x}", name: "full");
        });
        Assert.Equal("default:1",

        mapper.Map<int, string>(1));
        Assert.Equal("summary:1",
        mapper.Map<int, string>(1, name: "summary"));
        Assert.Equal("full:1",
        mapper.Map<int, string>(1, name: "full"));
    }

    [Fact]
    public void Map_NamedMapping_NotFound_ThrowsEvenIfDefaultExists()
    {
        var mapper = CreateMapper(m =>
            Add(m, (int x) => x.ToString()));

        var act = () => mapper.Map<int, string>(1, name: "nonexistent");

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Add_Generic_ReplacesExistingDelegate()
    {
        var mapper = CreateMapper(m =>
        {
            Add(m, (int x) => $"first:{x}");
            Add(m, (int x) => $"second:{x}");
        });
        Assert.Equal("second:1",

        mapper.Map<int, string>(1));
    }

    [Fact]
    public void OpenGenericSource_FallbackWorks()
    {
        var mapper = CreateMapper(m =>
            Add(m, typeof(List<>), typeof(int), (src, _) => ((IList)src).Count));

        var result = mapper.Map<List<string>, int>(["a", "b", "c"]);
        Assert.Equal(3,

        result);
    }

    [Fact]
    public void OpenGenericDest_FallbackWorks()
    {
        var mapper = CreateMapper(m =>
            Add(m, typeof(string), typeof(List<>), (src, _) => new List<string> { (string)src }));

        var result = mapper.Map(typeof(string), typeof(List<string>), "hello");

        var list = Assert.IsType<List<string>>(result);
        Assert.Equal("hello", Assert.Single(list));
    }

    [Fact]
    public void OpenGenericBoth_FallbackWorks()
    {
        var mapper = CreateMapper(o =>
            Add(o, typeof(List<>), typeof(HashSet<>), (src, _) =>
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

        Assert.IsType<HashSet<object>>(result);
    }

    [Fact]
    public void ClosedType_TakesPriorityOverOpenGeneric()
    {
        var mapper = CreateMapper(m =>
        {
            Add(m, typeof(List<>), typeof(int), (src, _) => 999);
            Add(m, typeof(List<string>), typeof(int), (src, _) => 42);
        });

        var result = mapper.Map<List<string>, int>(["a"]);
        Assert.Equal(42,
        result);

        var result2 = mapper.Map<List<int>, int>([1, 2]);
        Assert.Equal(999,
        result2);
    }

    [Fact]
    public void NullableFallback_MappingToNullableT_UsesRegisteredTMapping()
    {
        var mapper = CreateMapper(m =>
            Add(m, (string s) => s.Length));

        var found = mapper.TryMap<string, int?>("hello", out var result);
        Assert.True(

        found);
        Assert.Equal(5,
        result);
    }

    [Fact]
    public void NullableFallback_MappingToT_DoesNotUseNullableTMapping()
    {
        var mapper = CreateMapper(m =>
            Add(m, (string s) => (int?)s.Length));

        Action act = () => { _ = mapper.Map<string, int>("hello"); };

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void NullableFallback_DirectNullableRegistration_TakesPriority()
    {
        var mapper = CreateMapper(m =>
        {
            Add(m, (string s) => s.Length);
            Add(m, (string s) => (int?)(s.Length * 10));
        });

        var result = mapper.Map<string, int?>("hi");
        Assert.Equal(20,
        result);

        var result2 = mapper.Map<string, int>("hi");
        Assert.Equal(2,
        result2);
    }

    [Fact]
    public void NullableFallback_NoRegistration_ThrowsInvalidOperationException()
    {
        var mapper = CreateMapper();

        Action act = () => { _ = mapper.Map<string, int?>("hello"); };

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Map_UseRuntimeType_ResolvesUsingActualType()
    {
        var mapper = CreateMapper(m =>
            Add(m, (DerivedSource d) => new TargetDto($"derived:{d.Extra}")));

        BaseSource source = new DerivedSource { Name = "base", Extra = "ext" };

        var act = () => mapper.Map<BaseSource, TargetDto>(source);
        Assert.Throws<InvalidOperationException>(act);

        var result = mapper.Map<BaseSource, TargetDto>(source, useRuntimeType: true);
        Assert.Equal("derived:ext",
        result.Label);
    }

    [Fact]
    public void TryMap_UseRuntimeType_ResolvesUsingActualType()
    {
        var mapper = CreateMapper(m =>
            Add(m, (DerivedSource d) => new TargetDto($"derived:{d.Extra}")));

        BaseSource source = new DerivedSource { Name = "base", Extra = "ext" };

        var withoutFound = mapper.TryMap<BaseSource, TargetDto>(source, out var without);
        Assert.False(
        withoutFound);

        var withFound = mapper.TryMap<BaseSource, TargetDto>(source, out var with, useRuntimeType: true);
        Assert.True(
        withFound);
        Assert.Equal("derived:ext",
        with.Label);
    }

    [Fact]
    public void Add_WithMapper_DelegateCanUseMapperForNestedMapping()
    {
        var mapper = CreateMapper(m =>
        {
            Add(m, (int x) => $"mapped:{x}");
            Add(m, (Wrapper<int> src, IMapper mm) => $"wrapped({mm.Map<int, string>(src.Inner)})");
        });

        var wrapped = new Wrapper<int>(5);
        Assert.Equal("wrapped(mapped:5)",
        mapper.Map<Wrapper<int>, string>(wrapped));
    }

    [Fact]
    public void Map_NonGeneric_NullSourceType_ThrowsArgumentNullException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map(null!, typeof(string), "test");
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Map_NonGeneric_NullDestType_ThrowsArgumentNullException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map(typeof(string), null!, "test");
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Map_NonGeneric_NullSource_ThrowsArgumentNullException()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map(typeof(string), typeof(int), null!);
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void NoBuiltInMappings_IntToString_Throws()
    {
        var mapper = CreateMapper();

        var act = () => mapper.Map<int, string>(42);
        Assert.Throws<InvalidOperationException>(act);
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


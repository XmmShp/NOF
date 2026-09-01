using Microsoft.EntityFrameworkCore;
using NOF.Application;
using System.Linq.Expressions;
using Xunit;

namespace NOF.Infrastructure.Tests.Services;

public class ExpressionMapperTests
{
    [Fact]
    public void Map_UsesRegisteredExpression()
    {
        var mapper = CreateMapper(registry =>
            registry.Add(MappingRegistration.Of<int, string>(value => value.ToString())));

        Assert.Equal("42", mapper.Map<int, string>(42));
    }

    [Fact]
    public void GetExpression_ReturnsQueryableExpression()
    {
        var mapper = CreateMapper(registry =>
            registry.Add(MappingRegistration.Of<Source, Destination>(
                source => new Destination(source.Id, source.Name))));

        var expression = mapper.GetExpression<Source, Destination>();
        var result = new[] { new Source(7, "seven") }
            .AsQueryable()
            .Select(expression)
            .Single();

        Assert.Equal(new Destination(7, "seven"), result);
    }

    [Fact]
    public void ProjectTo_AppliesRegisteredExpression()
    {
        var mapper = CreateMapper(registry =>
            registry.Add(MappingRegistration.Of<Source, Destination>(
                source => new Destination(source.Id, source.Name))));

        var expression = new[] { new Source(3, "three") }
            .AsQueryable()
            .ProjectTo<Destination>(mapper)
            .Expression;
        var result = new[] { new Source(3, "three") }
            .AsQueryable()
            .ProjectTo<Destination>(mapper)
            .Single();

        Assert.Contains(nameof(Queryable.Select), expression.ToString());
        Assert.Equal(new Destination(3, "three"), result);
    }

    [Fact]
    public async Task ProjectTo_TranslatesAndPrunesColumnsWithEfCoreSqlite()
    {
        var options = new DbContextOptionsBuilder<ProjectionDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var dbContext = new ProjectionDbContext(options);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Entities.Add(new ProjectionEntity
        {
            Id = 1,
            Name = "projected",
            Unused = "must not be selected",
            Detail = new ProjectionDetail
            {
                Id = 2,
                Value = "nested",
                Unused = "nested value must not be selected"
            }
        });
        await dbContext.SaveChangesAsync();

        var mapper = CreateMapper(registry =>
        {
            registry.Add(MappingRegistration.Of<ProjectionDetail, ProjectionDetailDto>(
                detail => new ProjectionDetailDto(detail.Value)));
            registry.Add(MappingRegistration.Of<ProjectionEntity, ProjectionDto>(
                entity => new ProjectionDto(
                    entity.Id,
                    entity.Name,
                    MappingReference.Map<ProjectionDetail, ProjectionDetailDto>(entity.Detail))));
        });
        var query = dbContext.Entities
            .Where(entity => entity.Id == 1)
            .ProjectTo<ProjectionDto>(mapper);

        var sql = query.ToQueryString();
        var result = await EntityFrameworkQueryableExtensions.SingleAsync(query);

        Assert.Equal(new ProjectionDto(1, "projected", new ProjectionDetailDto("nested")), result);
        Assert.Contains("\"Id\"", sql);
        Assert.Contains("\"Name\"", sql);
        Assert.DoesNotContain("\"Unused\"", sql);
    }

    [Fact]
    public void NamedMappings_AreResolvedExactly()
    {
        var mapper = CreateMapper(registry =>
        {
            registry.Add(MappingRegistration.Of<int, string>(value => value.ToString()));
            registry.Add(MappingRegistration.Of<int, string>(value => "summary:" + value, "summary"));
        });

        Assert.Equal("1", mapper.Map<int, string>(1));
        Assert.Equal("summary:1", mapper.Map<int, string>(1, "summary"));
        Assert.Throws<InvalidOperationException>(() => mapper.Map<int, string>(1, "missing"));
    }

    [Fact]
    public void NamedProjection_IsResolvedExactly()
    {
        var mapper = CreateMapper(registry =>
        {
            registry.Add(MappingRegistration.Of<int, string>(value => value.ToString()));
            registry.Add(MappingRegistration.Of<int, string>(value => "summary:" + value, "summary"));
        });

        var result = new[] { 3 }
            .AsQueryable()
            .ProjectTo<string>(mapper, "summary")
            .Single();

        Assert.Equal("summary:3", result);
    }

    [Fact]
    public void MissingMapping_ThrowsDescriptiveException()
    {
        var mapper = CreateMapper();

        var exception = Assert.Throws<InvalidOperationException>(
            () => mapper.GetExpression<Source, Destination>());

        Assert.Contains(typeof(Source).FullName!, exception.Message);
        Assert.Contains(typeof(Destination).FullName!, exception.Message);
    }

    [Fact]
    public void LastRegistrationForSameKeyWins()
    {
        var mapper = CreateMapper(registry =>
        {
            registry.Add(MappingRegistration.Of<int, string>(value => "first:" + value));
            registry.Add(MappingRegistration.Of<int, string>(value => "second:" + value));
        });

        Assert.Equal("second:1", mapper.Map<int, string>(1));
    }

    [Fact]
    public void NestedMappingReference_IsInlinedForProjectionAndMap()
    {
        var mapper = CreateMapper(registry =>
        {
            registry.Add(MappingRegistration.Of<Child, ChildDto>(
                child => new ChildDto(child.Value)));
            registry.Add(MappingRegistration.Of<Parent, ParentDto>(
                parent => new ParentDto(
                    MappingReference.Map<Child, ChildDto>(parent.Child))));
        });

        var expression = mapper.GetExpression<Parent, ParentDto>();
        var mapped = mapper.Map<Parent, ParentDto>(new Parent(new Child("nested")));

        Assert.DoesNotContain(nameof(MappingReference), expression.ToString());
        Assert.DoesNotContain("Invoke", expression.ToString());
        Assert.Equal("nested", mapped.Child.Value);
    }

    [Fact]
    public void NestedNamedMappingReference_IsInlined()
    {
        var mapper = CreateMapper(registry =>
        {
            registry.Add(MappingRegistration.Of<Child, ChildDto>(
                child => new ChildDto("named:" + child.Value),
                "summary"));
            registry.Add(MappingRegistration.Of<Parent, ParentDto>(
                parent => new ParentDto(
                    MappingReference.Map<Child, ChildDto>(parent.Child, "summary"))));
        });

        var mapped = mapper.Map<Parent, ParentDto>(new Parent(new Child("value")));

        Assert.Equal("named:value", mapped.Child.Value);
    }

    [Fact]
    public void MissingNestedMapping_FailsWhenMapperIsCreated()
    {
        var registry = new MappingRegistry();
        registry.Add(MappingRegistration.Of<Parent, ParentDto>(
            parent => new ParentDto(
                MappingReference.Map<Child, ChildDto>(parent.Child))));

        var exception = Assert.Throws<InvalidOperationException>(() => new ExpressionMapper(registry));

        Assert.Contains(typeof(Child).FullName!, exception.Message);
        Assert.Contains(typeof(ChildDto).FullName!, exception.Message);
    }

    [Fact]
    public void CircularNestedMappings_FailWithDependencyPath()
    {
        var registry = new MappingRegistry();
        registry.Add(MappingRegistration.Of<CircularA, CircularADto>(
            source => new CircularADto(
                MappingReference.Map<CircularB, CircularBDto>(source.Child))));
        registry.Add(MappingRegistration.Of<CircularB, CircularBDto>(
            source => new CircularBDto(
                MappingReference.Map<CircularA, CircularADto>(source.Parent))));

        var exception = Assert.Throws<InvalidOperationException>(() => new ExpressionMapper(registry));

        Assert.Contains("Circular mapping dependency", exception.Message);
        Assert.Contains(typeof(CircularA).FullName!, exception.Message);
        Assert.Contains(typeof(CircularB).FullName!, exception.Message);
    }

    [Fact]
    public void ExpressionInvoke_IsRejected()
    {
        Expression<Func<Source, Destination>> nested =
            source => new Destination(source.Id, source.Name);
        var parameter = Expression.Parameter(typeof(Source), "source");
        var invoked = Expression.Lambda<Func<Source, Destination>>(
            Expression.Invoke(nested, parameter),
            parameter);
        var registry = new MappingRegistry();
        registry.Add(MappingRegistration.Of(invoked));

        var exception = Assert.Throws<InvalidOperationException>(() => new ExpressionMapper(registry));

        Assert.Contains("Expression.Invoke is not allowed", exception.Message);
    }

    [Fact]
    public void CapturedServiceConstant_IsRejected()
    {
        var prefixer = new Prefixer("captured:");
        var registry = new MappingRegistry();
        registry.Add(MappingRegistration.Of<int, string>(value => prefixer.Apply(value)));

        var exception = Assert.Throws<InvalidOperationException>(() => new ExpressionMapper(registry));

        Assert.Contains("captured constant", exception.Message);
    }

    private static ExpressionMapper CreateMapper(Action<MappingRegistry>? configure = null)
    {
        var registry = new MappingRegistry();
        configure?.Invoke(registry);
        return new ExpressionMapper(registry);
    }

    private sealed record Source(int Id, string Name);

    private sealed record Destination(int Id, string Name);

    private sealed record Child(string Value);

    private sealed record ChildDto(string Value);

    private sealed record Parent(Child Child);

    private sealed record ParentDto(ChildDto Child);

    private sealed record CircularA(CircularB Child);

    private sealed record CircularB(CircularA Parent);

    private sealed record CircularADto(CircularBDto Child);

    private sealed record CircularBDto(CircularADto Parent);

    private sealed class Prefixer(string prefix)
    {
        public string Apply(int value) => prefix + value;
    }

    private sealed class ProjectionEntity
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public required string Unused { get; set; }

        public required ProjectionDetail Detail { get; set; }
    }

    private sealed class ProjectionDetail
    {
        public int Id { get; set; }

        public required string Value { get; set; }

        public required string Unused { get; set; }
    }

    private sealed record ProjectionDto(int Id, string Name, ProjectionDetailDto Detail);

    private sealed record ProjectionDetailDto(string Value);

    private sealed class ProjectionDbContext(DbContextOptions<ProjectionDbContext> options) : DbContext(options)
    {
        public DbSet<ProjectionEntity> Entities => Set<ProjectionEntity>();
    }
}

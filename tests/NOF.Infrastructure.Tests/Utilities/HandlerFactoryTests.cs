using FluentAssertions;
using MassTransit.Mediator;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace NOF.Infrastructure.Tests.Utilities;

public class HandlerFactory_Create_Tests
{
    [Fact]
    public void Create_With_NonGeneric_Request_And_isQueryTrue_Should_Return_HandleGetAsync_Delegate()
    {
        // Act
        var del = HandlerFactory.Create(typeof(QueryWithoutResult), isQuery: true);

        // Assert
        del.Should().NotBeNull();
        del.Method.Name.Should().Be("HandleGetAsync");
        del.Method.GetParameters().Should().HaveCount(2);
        del.Method.ReturnType.Should().Be<Task<IResult>>();

        del.GetType().Should().Be<Func<QueryWithoutResult, IScopedMediator, Task<IResult>>>();
    }

    [Fact]
    public void Create_With_Generic_Request_And_isQueryTrue_Should_Return_HandleGetWithResultAsync_Delegate()
    {
        // Act
        var del = HandlerFactory.Create(typeof(QueryWithResult), isQuery: true);

        // Assert
        del.Should().NotBeNull();
        del.Method.Name.Should().Be("HandleGetWithResultAsync");
        del.Method.GetGenericArguments().Should().HaveCount(2);
        del.Method.GetGenericArguments()[0].Should().Be<QueryWithResult>();
        del.Method.GetGenericArguments()[1].Should().Be<string>();

        del.GetType().Should().Be<Func<QueryWithResult, IScopedMediator, Task<IResult>>>();
    }

    [Fact]
    public void Create_With_NonGeneric_Request_And_isQueryFalse_Should_Return_HandleCommandAsync_Delegate()
    {
        // Act
        var del = HandlerFactory.Create(typeof(CommandWithoutResult), isQuery: false);

        // Assert
        del.Should().NotBeNull();
        del.Method.Name.Should().Be("HandleCommandAsync");
        del.GetType().Should().Be<Func<CommandWithoutResult, IScopedMediator, Task<IResult>>>();
    }

    [Fact]
    public void Create_With_Generic_Request_And_isQueryFalse_Should_Return_HandleCommandWithResultAsync_Delegate()
    {
        // Act
        var del = HandlerFactory.Create(typeof(CommandWithResult), isQuery: false);

        // Assert
        del.Should().NotBeNull();
        del.Method.Name.Should().Be("HandleCommandWithResultAsync");
        del.Method.GetGenericArguments()[1].Should().Be<Guid>();
        del.GetType().Should().Be<Func<CommandWithResult, IScopedMediator, Task<IResult>>>();
    }
}


public class QueryWithoutResult : IRequest
{
    public string Filter { get; set; } = string.Empty;
}

public class QueryWithResult : IRequest<string>
{
    public int Id { get; set; }
}

public class CommandWithoutResult : IRequest
{
    public object Payload { get; set; } = new();
}

public class CommandWithResult : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
}

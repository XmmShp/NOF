using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NOF.Test;
using System.Text.Json;
using Xunit;

namespace NOF.Infrastructure.Tests.Middlewares;

public class ApiResponseMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_OpenApiRequest_ShouldNotWrapResponse()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ResponseWrapperMiddleware>>();
        var middleware = new ResponseWrapperMiddleware(mockLogger.Object);
        var context = HttpContextExtensions.CreateTestHttpContext();
        context.Request.Path = "/swagger/v1/swagger.json";

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_OptionsRequest_ShouldNotWrapResponse()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ResponseWrapperMiddleware>>();
        var middleware = new ResponseWrapperMiddleware(mockLogger.Object);
        var context = HttpContextExtensions.CreateTestHttpContext();
        context.Request.Method = "OPTIONS";

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SignalRRequest_ShouldNotWrapResponse()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ResponseWrapperMiddleware>>();
        var middleware = new ResponseWrapperMiddleware(mockLogger.Object);
        var context = HttpContextExtensions.CreateTestHttpContext();
        context.Request.Path = "/hubs/chat";

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulJsonResponse_ShouldWrapInResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ResponseWrapperMiddleware>>();
        var middleware = new ResponseWrapperMiddleware(mockLogger.Object);
        var context = HttpContextExtensions.CreateTestHttpContext();

        Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            var data = new { Name = "Test" };
            var json = JsonSerializer.Serialize(data);
            return ctx.Response.WriteAsync(json);
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        var responseBody = await context.GetResponseAsStringAsync();
        responseBody.Should().Contain("IsSuccess");
        responseBody.Should().Contain("true");
        responseBody.Should().Contain("Test");
    }

    [Fact]
    public async Task InvokeAsync_EmptySuccessResponse_ShouldReturnSuccessResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ResponseWrapperMiddleware>>();
        var middleware = new ResponseWrapperMiddleware(mockLogger.Object);
        var context = HttpContextExtensions.CreateTestHttpContext();

        Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        var responseBody = await context.GetResponseAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<Result>(responseBody, options);
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AlreadyResultType_ShouldNotDoubleWrap()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ResponseWrapperMiddleware>>();
        var middleware = new ResponseWrapperMiddleware(mockLogger.Object);
        var context = HttpContextExtensions.CreateTestHttpContext();

        Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            var result = new Result { IsSuccess = true };
            var json = JsonSerializer.Serialize(result);
            return ctx.Response.WriteAsync(json);
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        var responseBody = await context.GetResponseAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<Result>(responseBody, options);
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ErrorStatusCode_ShouldReturnErrorResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ResponseWrapperMiddleware>>();
        var middleware = new ResponseWrapperMiddleware(mockLogger.Object);
        var context = HttpContextExtensions.CreateTestHttpContext();

        Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        var responseBody = await context.GetResponseAsStringAsync();
        var result = JsonSerializer.Deserialize<Result>(responseBody, DefaultJsonSerializerOptions.Options);
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(404);
        context.Response.StatusCode.Should().Be(200); // Always returns 200
    }

    [Fact]
    public async Task InvokeAsync_Exception_ShouldReturnErrorResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ResponseWrapperMiddleware>>();
        var middleware = new ResponseWrapperMiddleware(mockLogger.Object);
        var context = HttpContextExtensions.CreateTestHttpContext();

        Task Next(HttpContext ctx)
        {
            throw new Exception("Test exception");
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        var responseBody = await context.GetResponseAsStringAsync();
        var result = JsonSerializer.Deserialize<Result>(responseBody, DefaultJsonSerializerOptions.Options);
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(500);
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_RedirectResponse_ShouldNotWrap()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ResponseWrapperMiddleware>>();
        var middleware = new ResponseWrapperMiddleware(mockLogger.Object);
        var context = HttpContextExtensions.CreateTestHttpContext();

        Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 302;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        context.Response.StatusCode.Should().Be(302);
    }

    [Fact]
    public void IsOpenApiRequest_SwaggerPath_ShouldReturnTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/swagger/index.html";

        // Act
        var result = ResponseWrapperMiddleware.IsOpenApiRequest(context.Request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsOpenApiRequest_OpenApiPath_ShouldReturnTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/openapi/v1/openapi.json";

        // Act
        var result = ResponseWrapperMiddleware.IsOpenApiRequest(context.Request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsOpenApiRequest_NormalPath_ShouldReturnFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/users";

        // Act
        var result = ResponseWrapperMiddleware.IsOpenApiRequest(context.Request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsOptionsRequest_OptionsMethod_ShouldReturnTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "OPTIONS";

        // Act
        var result = ResponseWrapperMiddleware.IsOptionsRequest(context.Request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsOptionsRequest_GetMethod_ShouldReturnFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";

        // Act
        var result = ResponseWrapperMiddleware.IsOptionsRequest(context.Request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSignalRRequest_HubsPath_ShouldReturnTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/hubs/chat";

        // Act
        var result = ResponseWrapperMiddleware.IsSignalRRequest(context.Request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSignalRRequest_NegotiatePath_ShouldReturnTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/chat/negotiate";

        // Act
        var result = ResponseWrapperMiddleware.IsSignalRRequest(context.Request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSignalRRequest_NormalPath_ShouldReturnFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/users";

        // Act
        var result = ResponseWrapperMiddleware.IsSignalRRequest(context.Request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsApiContentType_JsonContentType_ShouldReturnTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.ContentType = "application/json";

        // Act
        var result = ResponseWrapperMiddleware.IsApiContentType(context.Response);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsApiContentType_NullContentType_ShouldReturnTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var result = ResponseWrapperMiddleware.IsApiContentType(context.Response);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsApiContentType_HtmlContentType_ShouldReturnFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.ContentType = "text/html";

        // Act
        var result = ResponseWrapperMiddleware.IsApiContentType(context.Response);

        // Assert
        result.Should().BeFalse();
    }
}

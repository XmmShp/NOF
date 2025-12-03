using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace NOF.Contract.Tests.Extensions;

public class HttpClientExtensionsTests
{
    [Fact]
    public async Task GetJsonContent_WithConcreteObject_SerializesUsingRuntimeTypeAndRespectsOptions()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            UserName = "alice",
            Email = "alice@example.com",
            CreatedAt = new DateTime(2025, 12, 3, 10, 30, 0, DateTimeKind.Utc)
        };

        IRequest castRequest = request;

        // Act
        var jsonContent = HttpClientExtensions.GetJsonContent(castRequest);

        // Assert: Read the content as string and parse JSON
        var jsonString = await jsonContent.ReadAsStringAsync();
        jsonString.Should().NotBeNullOrWhiteSpace();

        // Parse to verify structure and naming policy
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        // Check camelCase (from JsonSerializerOptions)
        root.TryGetProperty("userName", out var userName).Should().BeTrue();
        userName.GetString().Should().Be("alice");

        root.TryGetProperty("email", out var email).Should().BeTrue();
        email.GetString().Should().Be("alice@example.com");

        root.TryGetProperty("createdAt", out var createdAt).Should().BeTrue();
        createdAt.GetString().Should().Be("2025-12-03T10:30:00Z"); // ISO 8601 UTC

        // Optional: Ensure it's not empty object
        root.EnumerateObject().Count().Should().Be(3);
    }

    // Example DTO
    public class CreateUserRequest : IRequest
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

using System.Security.Claims;
using Xunit;

namespace NOF.Abstraction.Tests;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void StandardJwtClaims_ShouldBePreferred()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "user-1"),
                new Claim("name", "Alice"),
                new Claim("email", "alice@example.com"),
                new Claim("sid", "session-1")
            ],
            authenticationType: "jwt",
            nameType: "name",
            roleType: ClaimTypes.Role));

        Assert.Equal("user-1", principal.Id);
        Assert.Equal("Alice", principal.Name);
        Assert.Equal("alice@example.com", principal.Email);
        Assert.Equal("session-1", principal.SessionId);
    }

    [Fact]
    public void LegacyDotNetClaims_ShouldStillFallback()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-2"),
                new Claim(ClaimTypes.Name, "Bob"),
                new Claim(ClaimTypes.Email, "bob@example.com"),
                new Claim(ClaimTypes.Sid, "session-2")
            ],
            authenticationType: "legacy"));

        Assert.Equal("user-2", principal.Id);
        Assert.Equal("Bob", principal.Name);
        Assert.Equal("bob@example.com", principal.Email);
        Assert.Equal("session-2", principal.SessionId);
    }
}

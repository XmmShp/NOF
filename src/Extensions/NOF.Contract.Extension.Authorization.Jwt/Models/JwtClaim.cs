using System.Security.Claims;

namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Represents a JWT claim to be issued or returned by the JWT authority.
/// </summary>
public sealed record JwtClaim
{
    public JwtClaim()
    {
    }

    public JwtClaim(string type, string value)
    {
        Type = type;
        Value = value;
    }

    public JwtClaim(string type, string value, string? valueType)
        : this(type, value)
    {
        ValueType = valueType;
    }

    /// <summary>
    /// Gets or sets the claim type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the claim value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the claim value type, such as <see cref="ClaimValueTypes.Integer64"/>.
    /// </summary>
    public string? ValueType { get; set; }

    public static JwtClaim Integer64(string type, long value)
    {
        return new JwtClaim(type, value.ToString(), ClaimValueTypes.Integer64);
    }
}

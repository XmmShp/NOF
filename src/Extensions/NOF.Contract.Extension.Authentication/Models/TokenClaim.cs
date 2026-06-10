using System.Security.Claims;

namespace NOF.Contract.Extension.Authentication;

/// <summary>
/// Represents a JWT claim to be issued or returned by the JWT authority.
/// </summary>
public sealed record TokenClaim
{
    public TokenClaim()
    {
    }

    public TokenClaim(string type, string value)
    {
        Type = type;
        Value = value;
    }

    public TokenClaim(string type, string value, string? valueType)
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

    public static TokenClaim Integer64(string type, long value)
    {
        return new TokenClaim(type, value.ToString(), ClaimValueTypes.Integer64);
    }
}

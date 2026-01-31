using Microsoft.Extensions.Options;

namespace NOF;

/// <summary>
/// JWT authentication configuration options.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Gets or sets the issuer of the JWT tokens.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the audience of the JWT tokens.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the security key for signing tokens.
    /// </summary>
    public string SecurityKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the access token expiration in minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the refresh token expiration in days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the clock skew for token validation.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = NOFJwtConstants.Expiration.DefaultClockSkew;

    /// <summary>
    /// Gets or sets the algorithm for JWT signing.
    /// </summary>
    public string Algorithm { get; set; } = NOFJwtConstants.DefaultAlgorithm;

    /// <summary>
    /// Gets or sets the JWKS endpoint path.
    /// </summary>
    public string JwksPath { get; set; } = NOFJwtConstants.DefaultJwksPath;
}

/// <summary>
/// Options validation for JwtOptions.
/// </summary>
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
            failures.Add("Issuer is required.");

        if (string.IsNullOrWhiteSpace(options.Audience))
            failures.Add("Audience is required.");

        if (string.IsNullOrWhiteSpace(options.SecurityKey))
            failures.Add("SecurityKey is required.");

        if (options.AccessTokenExpirationMinutes <= 0)
            failures.Add("AccessTokenExpirationMinutes must be greater than 0.");

        if (options.RefreshTokenExpirationDays <= 0)
            failures.Add("RefreshTokenExpirationDays must be greater than 0.");

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

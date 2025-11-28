namespace NOF;

public class JwtOptions
{
    public required string JwtKey { get; set; } = "abcdefghijklmnopqrstuvwxyz1234567890";
    public required string Issuer { get; set; } = "unspecified";
    public required string Audience { get; set; } = "unspecified";
    public required int ExpirationMinutes { get; set; } = 10;
}

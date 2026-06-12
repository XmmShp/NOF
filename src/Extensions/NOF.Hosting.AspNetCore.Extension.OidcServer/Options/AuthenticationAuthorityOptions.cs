using System.ComponentModel.DataAnnotations;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public class AuthenticationAuthorityOptions
{
    [Required(ErrorMessage = "Issuer is required.")]
    public string Issuer { get; set; } = string.Empty;

    [Range(2048, 4096, ErrorMessage = "KeySize must be between 2048 and 4096.")]
    public int KeySize { get; set; } = 2048;

    [Range(0, 10, ErrorMessage = "RetiredKeyRetentionCount must be between 0 and 10.")]
    public int RetiredKeyRetentionCount { get; set; } = 2;

    public string SigningKeyEncryptionKey { get; set; } = string.Empty;

    public TimeSpan KeyRotationInterval { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan RevokedRefreshTokenCleanupInterval { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan SigningKeyCleanupInterval { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan RevokedSigningKeyRetention { get; set; } = TimeSpan.FromDays(30);
}

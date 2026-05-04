using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class PersistenceRevokedRefreshTokenRepository : IRevokedRefreshTokenRepository
{
    private readonly DbContext _dbContext;

    public PersistenceRevokedRefreshTokenRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RevokeAsync(string tokenId, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var expiresAtUtc = DateTime.UtcNow.Add(expiration);
        var revokedToken = await _dbContext
            .FindAsync<RevokedRefreshToken>([tokenId], cancellationToken)
            .ConfigureAwait(false);

        if (revokedToken is null)
        {
            revokedToken = new RevokedRefreshToken
            {
                TokenId = tokenId,
                ExpiresAtUtc = expiresAtUtc
            };
            _dbContext.Set<RevokedRefreshToken>().Add(revokedToken);
        }
        else
        {
            revokedToken.ExpiresAtUtc = expiresAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var revokedToken = await _dbContext
            .FindAsync<RevokedRefreshToken>([tokenId], cancellationToken)
            .ConfigureAwait(false);

        return revokedToken is not null && revokedToken.ExpiresAtUtc > DateTime.UtcNow;
    }
}

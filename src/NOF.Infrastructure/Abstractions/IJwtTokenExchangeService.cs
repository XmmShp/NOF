using NOF.Hosting;

namespace NOF.Infrastructure;

public interface IJwtTokenExchangeService
{
    ValueTask<string> ExchangeTokenAsync(string subjectToken, JwtPropagation propagation, CancellationToken cancellationToken);
}

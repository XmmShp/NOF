using NOF.Hosting;

namespace NOF.Hosting;

public interface IJwtTokenExchangeService
{
    ValueTask<string> ExchangeTokenAsync(string subjectToken, JwtPropagation propagation, CancellationToken cancellationToken);
}

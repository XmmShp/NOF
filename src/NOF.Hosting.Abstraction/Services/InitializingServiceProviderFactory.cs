using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure;

public sealed class InitializingServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
{
    private readonly DefaultServiceProviderFactory _innerFactory = new();

    public IServiceCollection CreateBuilder(IServiceCollection services)
        => _innerFactory.CreateBuilder(services);

    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        => new InitializingServiceProvider(_innerFactory.CreateServiceProvider(containerBuilder));
}

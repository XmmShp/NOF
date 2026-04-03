using Microsoft.Extensions.DependencyInjection;

namespace NOF.Hosting;

public sealed class NOFServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
{
    private readonly DefaultServiceProviderFactory _innerFactory = new();

    public IServiceCollection CreateBuilder(IServiceCollection services)
        => _innerFactory.CreateBuilder(services);

    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        => new NOFServiceProvider(_innerFactory.CreateServiceProvider(containerBuilder));
}

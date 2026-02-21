using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

// ReSharper disable once InconsistentNaming
public static partial class NOFInfrastructureCoreExtensions
{
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Registers <typeparamref name="THandler"/> as a scope-aware delegating
        /// handler for this named/typed HTTP client. The handler will be resolved from
        /// the current DI scope (e.g. Blazor circuit scope) each time a client is created,
        /// giving it access to scoped services.
        /// <para>
        /// On first call this also replaces the default <see cref="IHttpClientFactory"/>
        /// with <see cref="ScopeAwareHttpClientFactory"/>. Clients that do not use this
        /// method are completely unaffected.
        /// </para>
        /// </summary>
        public IHttpClientBuilder AddScopeAwareHttpMessageHandler<THandler>()
            where THandler : DelegatingHandler
        {
            builder.Services.TryAddTransient<THandler>();
            builder.Services.Replace(ServiceDescriptor.Transient<IHttpClientFactory, ScopeAwareHttpClientFactory>());

            builder.Services.Configure<ScopeAwareHttpClientFactoryOptions>(
                builder.Name, options =>
                {
                    if (!options.HandlerTypes.Contains(typeof(THandler)))
                    {
                        options.HandlerTypes.Add(typeof(THandler));
                    }
                });

            return builder;
        }
    }
}

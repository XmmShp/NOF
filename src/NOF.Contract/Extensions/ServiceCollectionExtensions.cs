using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NOF;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">服务集合</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// 添加HTTP客户端，自动从配置中获取BaseAddress
        /// </summary>
        /// <typeparam name="TClient">客户端类型</typeparam>
        /// <param name="configurator">HttpClient的配置器</param>
        /// <returns>服务集合</returns>
        public IHttpClientBuilder AddHttpClientWithBaseAddress<TClient>(Action<IServiceProvider, HttpClient>? configurator = null)
            where TClient : class
        {
            var clientName = string.GetSystemNameFromClient<TClient>();

            return services.AddHttpClient<TClient>((sp, client) =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var baseAddress = configuration.GetConnectionString(clientName);
                if (baseAddress is not null)
                {
                    client.BaseAddress = new Uri(baseAddress);
                }
                configurator?.Invoke(sp, client);
            });
        }

        public OptionsBuilder<TOptions> AddOptionsInConfiguration<TOptions>(string? configSectionPath = null) where TOptions : class
        {
            // ReSharper disable once InvertIf
            if (string.IsNullOrEmpty(configSectionPath))
            {
                configSectionPath = string.GetSectionNameFromOptions<TOptions>();
            }

            return services.AddOptions<TOptions>()
                .BindConfiguration(configSectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
    }
}

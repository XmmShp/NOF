using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Infrastructure.Core;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NOF.Integration.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF.Hosting.AspNetCore;

public class NOFWebApplicationBuilder : NOFAppBuilder<WebApplication>
{
    public WebApplicationBuilder InnerBuilder { get; }

    protected NOFWebApplicationBuilder(string[] args)
    {
        InnerBuilder = WebApplication.CreateBuilder(args);
    }

    public static NOFWebApplicationBuilder Create(string[] args, bool useDefaultConfigs = true)
    {
        var builder = new NOFWebApplicationBuilder(args);
        builder.AddRegistrationStep(new AspNetCoreRegistrationStep());
        builder.AddRegistrationStep(new HttpHeaderInboundMiddlewareStep());
        if (useDefaultConfigs)
        {
            builder.UseDefaultSettings();
        }
        return builder;
    }

    /// <inheritdoc />
    protected override Task<WebApplication> BuildApplicationAsync()
    {
        return Task.FromResult(InnerBuilder.Build());
    }

    /// <inheritdoc />
    public override void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
    {
        ((IHostApplicationBuilder)InnerBuilder).ConfigureContainer(factory, configure);
    }

    /// <inheritdoc />
    public override IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

    /// <inheritdoc />
    public override IConfigurationManager Configuration => InnerBuilder.Configuration;

    /// <inheritdoc />
    public override IHostEnvironment Environment => InnerBuilder.Environment;

    /// <inheritdoc />
    public override ILoggingBuilder Logging => InnerBuilder.Logging;

    /// <inheritdoc />
    public override IMetricsBuilder Metrics => InnerBuilder.Metrics;

    /// <inheritdoc />
    public override IServiceCollection Services => InnerBuilder.Services;
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers <see cref="IIdGenerator"/> as a singleton backed by <see cref="SnowflakeIdGenerator"/>,
/// bound from the <c>NOF:Snowflake</c> configuration section. Added to the builder by default.
/// Use <c>builder.Services.Configure&lt;SnowflakeIdGeneratorOptions&gt;</c> to override values in code.
/// </summary>
internal sealed class SnowflakeIdGeneratorRegistrationStep : IServiceRegistrationStep<SnowflakeIdGeneratorRegistrationStep>
{
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "BindConfiguration is intercepted by EnableConfigurationBindingGenerator")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "BindConfiguration is intercepted by EnableConfigurationBindingGenerator")]
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddOptions<SnowflakeIdGeneratorOptions>()
            .BindConfiguration("NOF:Snowflake")
            .ValidateOnStart();

        builder.Services.AddSingleton<IIdGenerator>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SnowflakeIdGeneratorOptions>>().Value;
            return new SnowflakeIdGenerator(options);
        });

        return ValueTask.CompletedTask;
    }
}

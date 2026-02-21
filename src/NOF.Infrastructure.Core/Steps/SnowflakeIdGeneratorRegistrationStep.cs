using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers <see cref="IIdGenerator"/> as a singleton backed by <see cref="SnowflakeIdGenerator"/>,
/// bound from the <c>NOF:Snowflake</c> configuration section. Added to the builder by default.
/// Use <c>builder.Services.Configure&lt;SnowflakeIdGeneratorOptions&gt;</c> to override values in code.
/// </summary>
internal sealed class SnowflakeIdGeneratorRegistrationStep : IServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddOptionsInConfiguration<SnowflakeIdGeneratorOptions>("NOF:Snowflake");

        builder.Services.AddSingleton<IIdGenerator>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SnowflakeIdGeneratorOptions>>().Value;
            return new SnowflakeIdGenerator(options);
        });

        return ValueTask.CompletedTask;
    }
}

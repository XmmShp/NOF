using System.Runtime.CompilerServices;
using System.Text.Json;
using NOF.Contract;

namespace NOF.Contract.Tests;

internal static class ModuleInit
{
    [ModuleInitializer]
    internal static void Init()
    {
        JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
        {
            options.UseDefaultJsonTypeInfoResolver();
        });
    }
}

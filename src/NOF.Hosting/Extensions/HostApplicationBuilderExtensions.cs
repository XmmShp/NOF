using Microsoft.Extensions.Hosting;
using NOF.Abstraction;
using System.Reflection;

namespace NOF.Hosting;

public static partial class NOFHostingExtensions
{
    private static readonly object ApplicationPartsKey = new();

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddApplicationPart(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var loadedAssemblies = GetOrAddApplicationParts(builder);
            if (!loadedAssemblies.Add(assembly))
            {
                return builder;
            }

            foreach (var attribute in assembly.GetCustomAttributes<AssemblyInitializeAttribute>())
            {
                attribute.Initialize(builder.Services);
            }

            return builder;
        }
    }

    private static HashSet<Assembly> GetOrAddApplicationParts(IHostApplicationBuilder builder)
    {
        if (builder.Properties.TryGetValue(ApplicationPartsKey, out var existing) && existing is HashSet<Assembly> assemblies)
        {
            return assemblies;
        }

        assemblies = [];
        builder.Properties[ApplicationPartsKey] = assemblies;
        return assemblies;
    }
}

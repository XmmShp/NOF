using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

internal sealed class RpcOperationStartupValidatorHostedService(IServiceProvider serviceProvider) : IHostedService
{
    private const string ServiceImplementationGenericAttributeName = "NOF.Application.ServiceImplementationAttribute`1";

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Startup validation only; scans generated nested interfaces by reflection.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Startup validation only; assembly type scan is intentional.")]
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var missingOperations = new List<string>();
        foreach (var type in GetLoadableTypes())
        {
            if (!IsServiceImplementationType(type))
            {
                continue;
            }

            var operationContracts = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(t => t.IsInterface)
                .ToList();

            foreach (var operationContract in operationContracts)
            {
                if (serviceProvider.GetService(operationContract) is null)
                {
                    missingOperations.Add($"{type.FullName}.{operationContract.Name}");
                }
            }
        }

        if (missingOperations.Count > 0)
        {
            throw new InvalidOperationException(
                "Missing RPC operation implementations: " + string.Join(", ", missingOperations));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool IsServiceImplementationType(Type type)
        => type.GetCustomAttributes(inherit: false)
            .Any(a => a.GetType().IsGenericType
                      && a.GetType().GetGenericTypeDefinition().FullName == ServiceImplementationGenericAttributeName);

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Startup validation only; assembly type scan is intentional.")]
    private static IEnumerable<Type> GetLoadableTypes()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            foreach (var type in types)
            {
                yield return type;
            }
        }
    }
}

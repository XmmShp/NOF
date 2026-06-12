using Microsoft.Extensions.DependencyInjection;

namespace NOF.Abstraction;

public static partial class NOFAbstractionExtensions
{
    extension(IServiceCollection services)
    {
        public InitializedTypes InitializedTypes
            => AssemblyInitializationServices.GetOrAddSingleton<InitializedTypes>(services);
    }
}

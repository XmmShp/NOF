using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public class EFCoreConfigurator : IConfiguringServicesConfigurator
{
    public ValueTask ExecuteAsync(RegistrationArgs args)
    {
        args.Builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        return ValueTask.CompletedTask;
    }
}

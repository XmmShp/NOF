using MassTransit;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.MassTransit;

public readonly struct MassTransitSelector
{
    public INOFAppBuilder Builder { get; }
    private readonly MassTransitRegistrationStep _registrationStep;
    internal MassTransitSelector(INOFAppBuilder builder, MassTransitRegistrationStep registrationStep)
    {
        Builder = builder;
        _registrationStep = registrationStep;
    }

    public void ConfigureBus(Action<IBusRegistrationConfigurator> configure)
        => _registrationStep.ConfigureBus = configure;
}

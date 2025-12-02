namespace NOF;

public interface IConfiguringParametersConfigurator : IRegistrationConfigurator;
public interface IConfiguringOptionsConfigurator : IRegistrationConfigurator, IDepsOn<IConfiguringParametersConfigurator>;
public interface IConfiguredOptionsConfigurator : IRegistrationConfigurator, IDepsOn<IConfiguringOptionsConfigurator>;
public interface IConfiguringServicesConfigurator : IRegistrationConfigurator, IDepsOn<IConfiguredOptionsConfigurator>;
public interface IConfiguredServicesConfigurator : IRegistrationConfigurator, IDepsOn<IConfiguringServicesConfigurator>;

public interface ISyncSeedConfigurator : IStartupConfigurator;
public interface IObservabilityConfigurator : IStartupConfigurator, IDepsOn<ISyncSeedConfigurator>;
public interface ISecurityConfigurator : IStartupConfigurator, IDepsOn<IObservabilityConfigurator>;
public interface IAuthenticationConfigurator : IStartupConfigurator, IDepsOn<ISecurityConfigurator>;
public interface IBusinessConfigurator : IStartupConfigurator, IDepsOn<IAuthenticationConfigurator>;
public interface IEndpointConfigurator : IStartupConfigurator, IDepsOn<IBusinessConfigurator>;